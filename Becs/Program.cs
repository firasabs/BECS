using Becs.Data;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

static string ResolveCs(IConfiguration cfg)
{
    var fromCfg = cfg.GetConnectionString("DefaultConnection");
    var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    var raw = fromCfg ?? fromEnv;

    if (string.IsNullOrWhiteSpace(raw))
        return "Data Source=becs.db"; // local dev fallback

    // If someone supplied just a path ("/data/becs.db"), wrap it correctly
    if (!raw.Contains('='))
        return $"Data Source={raw.Trim()}";

    return raw;
}

var cs = ResolveCs(builder.Configuration);

// Optional: normalize via the builder to guarantee valid format
var sb = new SqliteConnectionStringBuilder(cs);
cs = sb.ToString();

builder.Services.AddSingleton<IIntakeRepository>(_ => new SqliteIntakeRepository(cs));
builder.Services.AddSingleton<IIssueRepository>(_ => new SqliteIssueRepository(cs));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<IUserRepository>(_ => new UserRepository(cs));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IResearcherData>(_ => new ResearcherData(cs));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Denied";
        options.Cookie.Name = "BECS.Auth";
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("admin"));
    options.AddPolicy("Staff", p => p.RequireRole("admin","user"));
    options.AddPolicy("Researcher", p => p.RequireRole("admin","researcher"));
});
var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok("OK"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}").RequireAuthorization();;

app.Run();