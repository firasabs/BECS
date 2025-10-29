using Microsoft.Extensions.ML;
using Microsoft.ML;

namespace Becs.ML;

public static class ModelBootstrap
{
    public const string DemandModelName = "DemandModel";
    public const string EligibilityModelName = "EligibilityModel";

    public static void AddMlModels(this IServiceCollection services, IConfiguration cfg)
    {
        var demandPath = cfg["Models:Demand"] ?? "/MLModel/demandModel.zip";
        var eligPath   = cfg["Models:Eligibility"] ?? "/MLModel/eligibilityModel.zip";

        Directory.CreateDirectory(Path.GetDirectoryName(demandPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(eligPath)!);

        services.AddPredictionEnginePool<DemandInput, DemandOutput>()
            .FromFile(DemandModelName, demandPath, watchForChanges: true);

        services.AddSingleton<MLContext>(_ =>
        {
            var ml = new MLContext();
            ml.ComponentCatalog.RegisterAssembly(typeof(Becs.ML.CondMapOut).Assembly);
            return ml;
        });
        // 2) Load the model once (singleton)
        services.AddSingleton<ITransformer>(sp =>
        {
            var ml = sp.GetRequiredService<MLContext>();
            var path = eligPath;
            using var fs = File.OpenRead(path);
            var model = ml.Model.Load(fs, out _);
            return model;
        });
        // 3) Create a PredictionEngine per scope (thread-safe for web requests)
        services.AddScoped<PredictionEngine<EligibilityInput, EligibilityOutput>>(sp =>
        {
            var ml    = sp.GetRequiredService<MLContext>();
            var model = sp.GetRequiredService<ITransformer>();
            return ml.Model.CreatePredictionEngine<EligibilityInput, EligibilityOutput>(model);
        });
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