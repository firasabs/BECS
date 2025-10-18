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
    public float EligiblePred { get; set; }      // probability (0..1) or score
    public string? Explanation { get; set; }     // if you attach it (e.g., from a custom transformer)
    public string? ModelVersion { get; set; }
}
