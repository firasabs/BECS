using Microsoft.ML.Data;

namespace Becs.ML;

public class DemandInput
{
    public float  Month { get; set; }
    public string BloodType { get; set; } = "";
    public string Rh { get; set; } = "";
}

public class DemandOutput
{
    [ColumnName("Score")]
    public float PredictedUnits { get; set; }
    public string? ModelVersion { get; set; } // Populate at scoring time if you want
}
