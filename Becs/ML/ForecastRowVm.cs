namespace Becs.ML;

public class ForecastRowVm
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string BloodType { get; set; } = "";
    public string Rh { get; set; } = "";
    public int PredictedUnits { get; set; }
    public string ModelVersion { get; set; } = "DemandModel";
}