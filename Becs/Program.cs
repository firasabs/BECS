using Becs.Data;
using Becs.ML;
using Becs.Services;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.ML;
using Microsoft.ML;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMlModels(builder.Configuration);
// canonical model paths (absolute, under content root)
string DemandZip = Path.Combine(builder.Environment.ContentRootPath, "MLModels", "demandModel.zip");
string EligZip   = Path.Combine(builder.Environment.ContentRootPath, "MLModels", "eligibilityModel.zip");

// ML.NET + pools
builder.Services.AddSingleton(_ => new MLContext(seed: 1));
builder.Services
    .AddPredictionEnginePool<DemandInput, DemandOutput>()
    .FromFile(modelName: "DemandModel", filePath: DemandZip, watchForChanges: true);
builder.Services
    .AddPredictionEnginePool<EligibilityInput, EligibilityOutput>()
    .FromFile(modelName: "EligibilityModel", filePath: EligZip, watchForChanges: true);

builder.Services.AddControllersWithViews();

static string ResolveCs(IConfiguration cfg)
{
    var fromCfg = cfg.GetConnectionString("DefaultConnection");
    var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    var raw = fromCfg ?? fromEnv;
    if (string.IsNullOrWhiteSpace(raw)) return "Data Source=becs.db";
    if (!raw.Contains('=')) return $"Data Source={raw.Trim()}";
    return raw;
}
var cs = new SqliteConnectionStringBuilder(ResolveCs(builder.Configuration)).ToString();

// DI registrations…
builder.Services.AddSingleton<IIntakeRepository>(_ => new SqliteIntakeRepository(cs));
builder.Services.AddSingleton<IIssueRepository>(_ => new SqliteIssueRepository(cs));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<IUserRepository>(_ => new UserRepository(cs));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IResearcherData>(_ => new ResearcherData(cs));
builder.Services.AddScoped<AiService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => {
        o.LoginPath = "/Auth/Login";
        o.LogoutPath = "/Auth/Logout";
        o.AccessDeniedPath = "/Auth/Denied";
        o.Cookie.Name = "BECS.Auth";
        o.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("AdminOnly", p => p.RequireRole("admin"));
    o.AddPolicy("Staff",     p => p.RequireRole("admin", "user"));
    o.AddPolicy("Researcher",p => p.RequireRole("admin", "researcher"));
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ---- TRAIN MODE ----
if (args.Contains("--train", StringComparer.OrdinalIgnoreCase))
{
    Directory.CreateDirectory(Path.GetDirectoryName(DemandZip)!);
    Directory.CreateDirectory(Path.GetDirectoryName(EligZip)!);

    Console.WriteLine($"[TRAIN] Using DB: {cs}");
    Console.WriteLine($"[TRAIN] Demand: {DemandZip}");
    Console.WriteLine($"[TRAIN] Elig:   {EligZip}");

    // call your trainers directly
    Trainers.TrainDemandFromSqlite(cs, DemandZip);
    Trainers.TrainEligibilityFromSqlite(cs, EligZip);

    Console.WriteLine("[TRAIN] Completed.");
    return; // exit after training
}

// ---- NORMAL RUN ----
// (Optionally auto-train on first run if files missing)
if (!File.Exists(DemandZip) || !File.Exists(EligZip))
{
    Directory.CreateDirectory(Path.GetDirectoryName(DemandZip)!);
    Directory.CreateDirectory(Path.GetDirectoryName(EligZip)!);
    Console.WriteLine("[BOOT] Model(s) missing. Training once at startup...");
    Trainers.TrainDemandFromSqlite(cs, DemandZip);
    Trainers.TrainEligibilityFromSqlite(cs, EligZip);
    Console.WriteLine("[BOOT] Training done.");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// quick smoke test for pools
app.MapGet("/ml/status",
    (PredictionEnginePool<DemandInput, DemandOutput> demandPool,
     PredictionEnginePool<EligibilityInput, EligibilityOutput> eligPool) =>
{
    try
    {
        _ = demandPool.Predict("DemandModel", new DemandInput());
        _ = eligPool.Predict("EligibilityModel", new EligibilityInput());
        return Results.Ok(new { demand = "ok", eligibility = "ok" });
    }
    catch (Exception ex)
    {
        return Results.Problem("PredictionEnginePool not ready: " + ex.Message);
    }
});

app.MapGet("/healthz", () => Results.Ok("OK"));
app.MapControllerRoute(name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
   .RequireAuthorization();

app.Run();
