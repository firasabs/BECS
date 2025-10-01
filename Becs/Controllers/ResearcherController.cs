using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[Authorize(Policy="Researcher")]
public class ResearcherController : Controller
{
    private readonly IResearcherData _data;
    private readonly IAuditLogger _audit; 

    public ResearcherController(IResearcherData data, IAuditLogger audit)
    {
        _data = data; _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rows = await _data.GetRowsAsync();

        await _audit.LogAsync(new AuditEntry
        {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            UserName = User.Identity?.Name,
            ActorType = "User",
            Action = "Researcher.Query",
            EntityName = "ResearcherView",
            DetailsJson = JsonSerializer.Serialize(new { Count = rows.Count }),
            Success = true
        });

        return View(rows);
    }
}