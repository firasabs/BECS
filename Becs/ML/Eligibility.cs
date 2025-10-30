using Microsoft.ML.Data;

namespace Becs.ML;

public class EligibilityInput
{
    public float Hb_g_dl { get; set; }
    public float Age { get; set; }
    public float Bp_Systolic { get; set; }
    public float Bp_Diastolic { get; set; }
    public float Days_Since_Last_Donation { get; set; }
    public string Conditions_Csv { get; set; } = "";
}

public class EligibilityOutput
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    // Present for most binary classifiers
    public float Probability { get; set; }

    public float Score { get; set; }
}
