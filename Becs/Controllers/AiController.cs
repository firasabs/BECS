using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Becs.Services;

namespace Becs.Controllers;

[Route("ai")]
public class AiController : Controller
{
    private readonly AiService _ai;
    private readonly IConfiguration _cfg;

    public AiController(AiService ai, IConfiguration cfg)
    {
        _ai = ai;
        _cfg = cfg;
    }

    private string ConnStr => _cfg.GetConnectionString("DefaultConnection") ?? "Data Source=/data/becs.db";

    // ---------------------------
    // 1) DEMAND FORECAST
    // ---------------------------

    // GET /ai/forecast?month=10&save=true -> returns View or JSON if header asks for JSON
    [HttpGet("forecast")]
    public IActionResult Forecast([FromQuery] int month = 1, [FromQuery] bool save = false, [FromQuery] int? year = null)
    {
        var items = _ai.PredictDemandForMonth(month)
            .Select(x => new { blood_type = x.bt, rh = x.rh, predicted_units = x.units, model_version = x.ver })
            .ToList();

        if (save)
        {
            var y = year ?? DateTime.UtcNow.Year;
            using var con = new SqliteConnection(ConnStr);
            con.Open();

            foreach (var it in items)
            {
                using var cmd = con.CreateCommand();
                cmd.CommandText = @"
INSERT OR REPLACE INTO Forecasts(year,month,blood_type,rh,predicted_units,model_version)
VALUES($year,$month,$bt,$rh,$u,$ver);";
                cmd.Parameters.AddWithValue("$year", y);
                cmd.Parameters.AddWithValue("$month", month);
                cmd.Parameters.AddWithValue("$bt", it.blood_type);
                cmd.Parameters.AddWithValue("$rh", it.rh);
                cmd.Parameters.AddWithValue("$u", it.predicted_units);
                cmd.Parameters.AddWithValue("$ver", it.model_version);
                cmd.ExecuteNonQuery();
            }
        }

        // If you're testing, JSON is convenient:
        if (Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return Json(items);

        return View(items); // Views/Ai/Forecast.cshtml (optional). If missing, return Json(items) instead.
    }

    // ---------------------------
    // 2) CROSS-MATCH
    // ---------------------------

    // POST /ai/crossmatch  (rb, rrh, db, drh) -> JSON
    [HttpPost("crossmatch")]
    public IActionResult CrossMatch([FromForm] string rb, [FromForm] string rrh, [FromForm] string db, [FromForm] string drh)
    {
        var (ok, why) = _ai.CheckRbcCompatibility(rb, rrh, db, drh);
        return Json(new { is_compatible = ok, rationale = why });
    }

    // ---------------------------
    // 3) ELIGIBILITY
    // ---------------------------

    public class EligibilityBody
    {
        public int donor_id { get; set; }
        public float hb_g_dl { get; set; }
        public int age { get; set; }
        public int bp_systolic { get; set; }
        public int bp_diastolic { get; set; }
        public int days_since_last_donation { get; set; }
        public string[] conditions { get; set; } = Array.Empty<string>();
        public bool save { get; set; } = true; // default: save to DB
    }

    // POST /ai/eligibility -> JSON; optionally saves to EligibilityPredictions
    [HttpPost("eligibility")]
    public IActionResult Eligibility([FromBody] EligibilityBody body)
    {
        var (eligible, proba, ver, why) =
            _ai.PredictEligibility(body.hb_g_dl, body.age, body.bp_systolic, body.bp_diastolic,
                                   body.days_since_last_donation, body.conditions);

        if (body.save)
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
INSERT INTO EligibilityPredictions(donor_id, eligible_pred, probability, model_version, explanation)
VALUES($id, $pred, $p, $ver, $exp);";
            cmd.Parameters.AddWithValue("$id", body.donor_id);
            cmd.Parameters.AddWithValue("$pred", eligible ? 1 : 0);
            cmd.Parameters.AddWithValue("$p", proba);
            cmd.Parameters.AddWithValue("$ver", ver);
            cmd.Parameters.AddWithValue("$exp", why);
            cmd.ExecuteNonQuery();
        }

        return Json(new { eligible_pred = eligible, probability = proba, model_version = ver, explanation = why });
    }
}
