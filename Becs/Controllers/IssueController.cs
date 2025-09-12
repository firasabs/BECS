using Becs.Models;
using Becs.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
namespace Becs.Controllers;

public class IssueController : Controller
{
    private readonly InventoryService _svc;
    public IssueController(InventoryService svc) => _svc = svc;

    public IActionResult Routine()
    {
        ViewBag.Result = null;
        return View(new RoutineIssueInput());
    }
    [HttpPost]
    public IActionResult Routine(RoutineIssueInput input)
    {
        if (input.Quantity <= 0) ModelState.AddModelError("", "כמות חייבת להיות חיובית.");
        if (!new[] { "O","A","B","AB" }.Contains(input.ABO?.ToUpperInvariant()))
            ModelState.AddModelError("", "ABO לא תקין.");
        if (input.RhSign != "+" && input.RhSign != "-")
            ModelState.AddModelError("", "Rh לא תקין.");

        if (!ModelState.IsValid) return View(input);

        var req = new BloodType(input.ABO.ToUpperInvariant(), input.RhSign == "-" ? Rh.Neg : Rh.Pos);
        var (chosen, suggestions) = _svc.SelectForRoutine(req, input.Quantity);

        ViewBag.Suggestions = suggestions;
        ViewBag.Chosen = chosen;

        return View(input);
    }

    [HttpPost]
    public IActionResult ConfirmIssue([FromForm] string[] ids)
    {
        var issued = _svc.IssueByIds(ids, "routine");
        TempData["ok"] = $"הונפקו {issued.Count} מנות: {string.Join(", ", issued.Select(u => u.Id))}";
        return RedirectToAction(nameof(Routine));
    }

    public IActionResult Emergency()
    {
        var svc = _svc; // already injected
        // Count O- in stock for UX
        var oNeg = svc.AllUnits().Count(u => u.Type.ABO == "O" && u.Type.Rh == Becs.Models.Rh.Neg);
        ViewBag.ONegCount = oNeg;
        return View();
    }

    [HttpPost]
    public IActionResult EmergencyIssue()
    {
        var issued = _svc.IssueEmergencyONeg();
        if (issued.Count == 0) TempData["err"] = "אין מלאי O-! דרוש טיפול מיידי.";
        else TempData["ok"]  = $"נופקו {issued.Count} מנות O-: {string.Join(", ", issued.Select(u => u.Id))}";
        return RedirectToAction(nameof(Emergency));
    }
}