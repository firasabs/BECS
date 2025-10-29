using Becs.ML;
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
   public IActionResult Forecasts(int year = 2025, int month = 1, string horizon = "single", string source = "live")
{
    // horizon: "single" | "year"
    // source:  "live"   | "stored"
    var rows = new List<ForecastRowVm>();
    var months = horizon == "year" ? Enumerable.Range(1, 12) : new[] { month };

    if (string.Equals(source, "stored", StringComparison.OrdinalIgnoreCase))
    {
        // read from Forecasts table if present; fall back to live if empty
        using var con = new SqliteConnection(ConnStr);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Forecasts (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              year INTEGER NOT NULL,
              month INTEGER NOT NULL CHECK(month BETWEEN 1 AND 12),
              blood_type TEXT NOT NULL CHECK(blood_type IN ('O','A','B','AB')),
              rh TEXT NOT NULL CHECK(rh IN ('+','-')),
              predicted_units INTEGER NOT NULL,
              model_version TEXT NOT NULL,
              created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
              UNIQUE(year, month, blood_type, rh, model_version)
            );
            SELECT year, month, blood_type, rh, predicted_units, model_version
            FROM Forecasts
            WHERE year = $y AND month IN (" + string.Join(",", months) + @")
            ORDER BY month, blood_type, rh;";
        cmd.Parameters.AddWithValue("$y", year);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            rows.Add(new ForecastRowVm {
                Year = r.GetInt32(0),
                Month = r.GetInt32(1),
                BloodType = r.GetString(2),
                Rh = r.GetString(3),
                PredictedUnits = r.GetInt32(4),
                ModelVersion = r.GetString(5)
            });
        }
    }

    if (rows.Count == 0) // live compute (current behavior), for 1 or 12 months
    {
        foreach (var m in months)
        {
            var preds = _ai.PredictDemandForMonth(m).ToList(); // (bt,rh,units,ver)
            foreach (var p in preds)
            {
                rows.Add(new ForecastRowVm {
                    Year = year, Month = m, BloodType = p.bt, Rh = p.rh,
                    PredictedUnits = p.units, ModelVersion = p.ver
                });
            }
        }
    }

    ViewBag.Year = year;
    ViewBag.Month = month;
    ViewBag.Horizon = horizon; // "single" or "year"
    ViewBag.Source = source;   // "live" or "stored"
    return View(rows);
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
        public int? donor_id { get; set; }
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
        try
        {
            var (eligible, proba, ver, why) = _ai.PredictEligibility(
                body.hb_g_dl, body.age, body.bp_systolic, body.bp_diastolic,
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
                cmd.Parameters.AddWithValue("$ver", ver ?? "EligibilityModel");
                cmd.Parameters.AddWithValue("$exp", why ?? "Model decision.");
                cmd.ExecuteNonQuery();
            }

            return Ok(new { eligible_pred = eligible, probability = proba, model_version = ver, explanation = why });
        }
        catch (Exception ex)
        {
            // TEMP: surface details so you can see them in the browser console
            return Problem(ex.ToString());
        }
    }


}
