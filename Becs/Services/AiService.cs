using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Becs.ML;

namespace Becs.Services;

public class AiService
{
    // ⬅️ Two pools, one per model
    private readonly PredictionEnginePool<DemandInput, DemandOutput> _demandPool;
    private readonly PredictionEnginePool<EligibilityInput, EligibilityOutput> _eligPool;

    private readonly ILogger<AiService> _logger;
    private readonly IConfiguration _cfg;

    public AiService(
        PredictionEnginePool<DemandInput, DemandOutput> demandPool,
        PredictionEnginePool<EligibilityInput, EligibilityOutput> eligPool,
        ILogger<AiService> logger,
        IConfiguration cfg)
    {
        _demandPool = demandPool;
        _eligPool   = eligPool;
        _logger     = logger;
        _cfg        = cfg;
    }

    private string ConnStr => _cfg.GetConnectionString("DefaultConnection") ?? "Data Source=/data/becs.db";

    // ---------------------------
    // 1) DEMAND FORECAST
    // ---------------------------
    public IEnumerable<(string bt, string rh, int units, string ver)> PredictDemandForMonth(int month)
    {
        var bloodTypes = new[] { "A", "B", "AB", "O" };
        var rhs = new[] { "+", "-" };
        var results = new List<(string bt, string rh, int units, string ver)>();

        foreach (var bt in bloodTypes)
        foreach (var rh in rhs)
        {
            var input = new DemandInput { Month = month, BloodType = bt, Rh = rh };
            var pred  = _demandPool.Predict("DemandModel", input);   // ⬅️ use demand pool
            var units = (int)Math.Max(0, Math.Round(pred.PredictedUnits));
            results.Add((bt, rh, units, pred.ModelVersion ?? "DemandModel"));
        }
        return results;
    }

    // ---------------------------
    // 2) CROSS-MATCH (same as yours)
    // ---------------------------
    public (bool ok, string why) CheckRbcCompatibility(string rb, string rrh, string db, string drh)
    {
        try
        {
            using var con = new SqliteConnection(ConnStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
SELECT compatible
FROM CompatibilityMatrix
WHERE donor_blood = $db AND donor_rh = $drh
  AND recipient_blood = $rb AND recipient_rh = $rrh
LIMIT 1;";
            cmd.Parameters.AddWithValue("$db", db);
            cmd.Parameters.AddWithValue("$drh", drh);
            cmd.Parameters.AddWithValue("$rb", rb);
            cmd.Parameters.AddWithValue("$rrh", rrh);

            var res = cmd.ExecuteScalar();
            if (res is long i)
                return (i != 0, i != 0 ? "Compatible per matrix." : "Incompatible per matrix.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CompatibilityMatrix lookup failed; falling back to rules.");
        }

        bool aboOk = db switch
        {
            "O"  => true,
            "A"  => rb is "A" or "AB",
            "B"  => rb is "B" or "AB",
            "AB" => rb is "AB",
            _    => false
        };
        bool rhOk = drh == "-" || (drh == "+" && rrh == "+");
        var ok2 = aboOk && rhOk;
        return (ok2, ok2 ? "Compatible by ABO/Rh rules." : "Incompatible by ABO/Rh rules.");
    }

    // ---------------------------
    // 3) DONOR ELIGIBILITY
    // ---------------------------
    public (bool eligible, double proba, string ver, string why) PredictEligibility(
        float hb, int age, int bpSys, int bpDia, int daysSinceLast, string[] conditions)
    {
        var input = new EligibilityInput
        {
            Hb_g_dl = hb,
            Age = age,
            Bp_Systolic = bpSys,
            Bp_Diastolic = bpDia,
            Days_Since_Last_Donation = daysSinceLast,
            Conditions_Csv = string.Join(',', conditions)
            
        };

        var pred = _eligPool.Predict("EligibilityModel", input);   // ⬅️ use eligibility pool

        var eligible = pred.EligiblePred > 0.5f;
        var why = string.IsNullOrWhiteSpace(pred.Explanation) ? "Model decision." : pred.Explanation;
        return (eligible, pred.EligiblePred, pred.ModelVersion ?? "EligibilityModel", why);
    }
}
