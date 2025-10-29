using Microsoft.Data.Sqlite;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Data;
using Microsoft.ML.Transforms;
using Microsoft.ML.Transforms.Text;

namespace Becs.ML;

public static partial class Trainers
{
    // --- Demand regression ---
    public static void TrainDemandFromSqlite(string conn, string modelPath)
    {
        var ml = new MLContext(seed: 42);

        // Load data from SQLite -> in-memory IEnumerable
        var data = LoadMonthlyUsage(conn).ToList();
        if (data.Count == 0)
        {
            throw new InvalidOperationException("[Demand] No rows in MonthlyUsage; cannot train.");
        }
        Console.WriteLine($"[Demand] Training on {data.Count} rows.");
        var dv = ml.Data.LoadFromEnumerable(data);


        var split = ml.Data.TrainTestSplit(dv, testFraction: 0.2);

        var pipeline =
            ml.Transforms.Categorical.OneHotEncoding(nameof(DemandInput.BloodType))
            .Append(ml.Transforms.Categorical.OneHotEncoding(nameof(DemandInput.Rh)))
            .Append(ml.Transforms.Concatenate("Features", nameof(DemandInput.Month),
                                              nameof(DemandInput.BloodType),
                                              nameof(DemandInput.Rh)))
            .Append(ml.Regression.Trainers.FastTree(
                numberOfLeaves: 20,
                numberOfTrees: 200,
                minimumExampleCountPerLeaf: 10));

        // Label must be named "Label" for regression trainers
        var labeled = ml.Transforms.CopyColumns("Label", nameof(MonthlyUsageRow.UnitsIssued))
                                   .Fit(split.TrainSet).Transform(split.TrainSet);
        var testLabeled = ml.Transforms.CopyColumns("Label", nameof(MonthlyUsageRow.UnitsIssued))
                                        .Fit(split.TrainSet).Transform(split.TestSet);

        var model = pipeline.Fit(labeled);
        var metrics = ml.Regression.Evaluate(model.Transform(testLabeled));
        Console.WriteLine($"[Demand] MAE={metrics.MeanAbsoluteError:F2} RMSE={metrics.RootMeanSquaredError:F2} R2={metrics.RSquared:F3}");

        ml.Model.Save(model, labeled.Schema, modelPath);
        Console.WriteLine($"[Demand] Saved {modelPath}");
    }

    private static IEnumerable<DemandInputRow> LoadMonthlyUsage(string conn)
    {
        const string sql = @"
        SELECT month, blood_type, rh, units_issued
        FROM MonthlyUsage
        ORDER BY year, month, blood_type, rh;";

        using var c = new SqliteConnection(conn);
        c.Open();
        using var cmd = new SqliteCommand(sql, c);
        using var r = cmd.ExecuteReader();

        int rows = 0;
        while (r.Read())
        {
            rows++;
            yield return new DemandInputRow
            {
                Month        = r.GetInt32(0),     // 1..12
                BloodType    = r.GetString(1),    // 'O','A','B','AB'
                Rh           = r.GetString(2),    // '+','-'
                UnitsIssued  = r.GetInt32(3)
            };
        }
        Console.WriteLine($"[Demand] Loaded {rows} rows from MonthlyUse.");
    }


    private class DemandInputRow
    {
        public float Month { get; set; }
        public string BloodType { get; set; } = "";
        public string Rh { get; set; } = "";
        public float UnitsIssued { get; set; }
    }

    private class MonthlyUsageRow { public float UnitsIssued { get; set; } }

    // --- Eligibility: binary classification ---
public static void TrainEligibilityFromSqlite(string conn, string modelPath)
{
    var ml = new MLContext(seed: 42);

    // 1) Load rows into memory so we can stratify
    var list = LoadEligibilityRows(conn).ToList();
    if (list.Count == 0)
    {
        Console.WriteLine("[Eligibility] No rows found; synthesizing a tiny baseline set...");
        list = new List<EligibilityInputLabeled>
        {
            new() { Hb_g_dl=14,    Age=30, Bp_Systolic=120, Bp_Diastolic=80,  Days_Since_Last_Donation=120, Conditions_Csv="",              Label=true  },
            new() { Hb_g_dl=13.5f, Age=40, Bp_Systolic=118, Bp_Diastolic=76,  Days_Since_Last_Donation=90,  Conditions_Csv="none",          Label=true  },
            new() { Hb_g_dl=11,    Age=28, Bp_Systolic=150, Bp_Diastolic=95,  Days_Since_Last_Donation=5,   Conditions_Csv="flu",           Label=false },
            new() { Hb_g_dl=12.2f, Age=54, Bp_Systolic=145, Bp_Diastolic=92,  Days_Since_Last_Donation=30,  Conditions_Csv="hypertension",  Label=false }
        };
    }

    // 2) Stratified-ish split
    var pos = list.Where(x => x.Label).ToList();
    var neg = list.Where(x => !x.Label).ToList();
    if (pos.Count == 0 || neg.Count == 0)
        Console.WriteLine("[Eligibility] WARNING: single-class dataset; metrics may be limited.");

    int testPos = Math.Max(1, pos.Count / 5);
    int testNeg = Math.Max(1, neg.Count / 5);

    var testList  = pos.Take(testPos).Concat(neg.Take(testNeg)).ToList();
    var trainList = list.Except(testList).ToList();
    if (trainList.Count < 2) trainList = list; // safety

    Console.WriteLine($"[Eligibility] Train={trainList.Count} Test={testList.Count} " +
                      $"(posT={testList.Count(x=>x.Label)}, negT={testList.Count(x=>!x.Label)})");

    var dvTrain = ml.Data.LoadFromEnumerable(trainList);
    var dvTest  = ml.Data.LoadFromEnumerable(testList);

    // 3) Map Conditions_Csv -> CondFlag (0/1) to avoid text featurizer pitfalls
    //    NOTE: This keeps your AiService API unchanged (string in), but the pipeline
    //    internally converts it to a numeric feature.
    var map = new Action<CondMapIn, CondMapOut>((src, dst) =>
    {
        var s = src.Conditions_Csv?.Trim();
        dst.CondFlag = (string.IsNullOrEmpty(s) || s == "[]" || s == "[ ]") ? 0f : 1f;
    });

    var pipeline =
        ml.Transforms.CustomMapping(map, contractName: "CondFlagMap")
            .Append(ml.Transforms.Concatenate("Features",
                nameof(EligibilityInput.Hb_g_dl),
                nameof(EligibilityInput.Age),
                nameof(EligibilityInput.Bp_Systolic),
                nameof(EligibilityInput.Bp_Diastolic),
                nameof(EligibilityInput.Days_Since_Last_Donation),
                nameof(CondMapOut.CondFlag)))
            .Append(ml.BinaryClassification.Trainers.FastTree());

    var model = pipeline.Fit(dvTrain);

    // 4) Safe evaluation (AUC fails for single-class test)
    try
    {
        var metrics = ml.BinaryClassification.Evaluate(model.Transform(dvTest), labelColumnName: "Label");
        Console.WriteLine($"[Eligibility] AUC={metrics.AreaUnderRocCurve:F3} ACC={metrics.Accuracy:P2}");
    }
    catch (ArgumentOutOfRangeException)
    {
        var scored = ml.Data.CreateEnumerable<EligibilityScoreRow>(model.Transform(dvTest), reuseRowObject: false).ToList();
        var gt = testList.Select(x => x.Label).ToList();
        var acc = scored.Zip(gt, (p, y) => ((p.Probability >= 0.5f) == y) ? 1.0 : 0.0).Average();
        Console.WriteLine($"[Eligibility] AUC=N/A (single-class test). ACC≈{acc:P2}");
    }

    Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
    ml.Model.Save(model, dvTrain.Schema, modelPath);
    Console.WriteLine($"[Eligibility] Saved {modelPath}");
}

    // Public DTOs so ML.NET can reflect them at runtime
    public sealed class CondMapIn  { public string? Conditions_Csv { get; set; } }
    public sealed class CondMapOut { public float  CondFlag       { get; set; } }

    private static IEnumerable<EligibilityInputLabeled> LoadEligibilityRows(string conn)
    {
        const string sql = @"
        SELECT 
          COALESCE(hb_g_dl, 0),
          COALESCE(age, 0),
          COALESCE(bp_systolic, 0),
          COALESCE(bp_diastolic, 0),
          CASE 
            WHEN last_donation_date IS NULL THEN 999
            ELSE CAST((julianday('now') - julianday(last_donation_date)) AS INT)
          END AS days_since,
          COALESCE(conditions_json, '') AS conds,      
          CASE 
            WHEN eligible_label = 'Eligible' THEN 1 
            WHEN eligible_label = 'Not' OR eligible_label='Not Eligible' THEN 0
            ELSE NULL
          END AS label
        FROM DonorHealth
        WHERE label IS NOT NULL;";

        using var c = new SqliteConnection(conn);
        c.Open();
        using var cmd = new SqliteCommand(sql, c);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            yield return new EligibilityInputLabeled
            {
                Hb_g_dl = Convert.ToSingle(r.GetDouble(0)),
                Age = r.GetInt32(1),
                Bp_Systolic = r.GetInt32(2),
                Bp_Diastolic = r.GetInt32(3),
                Days_Since_Last_Donation = r.GetInt32(4),
                Conditions_Csv = r.IsDBNull(5) ? "" : r.GetString(5),  // <-- string
                Label = r.GetInt32(6) == 1
            };
        }
    }


    private class EligibilityInputLabeled : EligibilityInput
    {
        public bool Label { get; set; }
    }

    private sealed class EligibilityScoreRow
    {
        public bool  PredictedLabel { get; set; }
        public float Probability    { get; set; }
    }
}
