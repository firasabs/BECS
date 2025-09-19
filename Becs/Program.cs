using Becs.Data;
using Microsoft.Data.Sqlite;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
// SQLite repos
builder.Services.AddSingleton<IIntakeRepository, SqliteIntakeRepository>();
builder.Services.AddSingleton<IIssueRepository, SqliteIssueRepository>();
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok("OK"));


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();