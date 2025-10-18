using Microsoft.Extensions.ML;
using Microsoft.ML;

namespace Becs.ML;

public static class ModelBootstrap
{
    public const string DemandModelName = "DemandModel";
    public const string EligibilityModelName = "EligibilityModel";

    public static void AddMlModels(this IServiceCollection services, IConfiguration cfg)
    {
        var demandPath = cfg["Models:Demand"] ?? "/ML/demand_v1.zip";
        var eligPath   = cfg["Models:Eligibility"] ?? "/ML/eligibility_v1.zip";

        Directory.CreateDirectory(Path.GetDirectoryName(demandPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(eligPath)!);

        services.AddPredictionEnginePool<DemandInput, DemandOutput>()
            .FromFile(DemandModelName, demandPath, watchForChanges: true);

        services.AddPredictionEnginePool<EligibilityInput, EligibilityOutput>()
            .FromFile(EligibilityModelName, eligPath, watchForChanges: true);
        Console.WriteLine("Demand model path: " + Path.GetFullPath(demandPath));
        Console.WriteLine("Eligibility model path: " + Path.GetFullPath(eligPath));

    }

    public static void EnsureModelsTrained(IConfiguration cfg, string sqliteConn)
    {
        var demandPath = cfg["Models:Demand"] ?? "ML/demand_v1.zip";
        var eligPath   = cfg["Models:Eligibility"] ?? "ML/eligibility_v1.zip";

        if (!File.Exists(demandPath))
        {
            Console.WriteLine("[Bootstrap] Training Demand model...");
            Trainers.TrainDemandFromSqlite(sqliteConn, demandPath);
        }
        if (!File.Exists(eligPath))
        {
            Console.WriteLine("[Bootstrap] Training Eligibility model...");
            Trainers.TrainEligibilityFromSqlite(sqliteConn, eligPath);
        }
    }
}