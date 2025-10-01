// Controllers/IssueController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Becs.Models;
using Becs.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Becs.Controllers
{
    [Authorize(Roles = "admin,user")]
    public class IssueController : Controller
    {
        private readonly IIssueRepository _repo;
        public IssueController(IIssueRepository repo) => _repo = repo;

        [HttpGet]
        public IActionResult Routine()
        {
            ViewBag.Result = null;    // legacy
            return View(new RoutineIssueInput { Quantity = 1, RhSign = "+" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Routine(RoutineIssueInput input, CancellationToken ct)
        {
            // Validate
            var abo = input.ABO?.ToUpperInvariant();
            if (input.Quantity <= 0) ModelState.AddModelError("", "כמות חייבת להיות חיובית.");
            if (abo is null || !(new[] { "O","A","B","AB" }.Contains(abo)))
                ModelState.AddModelError("", "ABO לא תקין.");
            if (input.RhSign != "+" && input.RhSign != "-")
                ModelState.AddModelError("", "Rh לא תקין.");

            if (!ModelState.IsValid) return View(input);

            var (chosen, suggestions) = await _repo.SelectForRoutineAsync(abo!, input.RhSign, input.Quantity, ct);
            ViewBag.Suggestions = suggestions;   // List<AltSuggestion>
            ViewBag.Chosen = chosen;             // List<BloodUnitVm>

            return View(input);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmIssue(List<string> ids, CancellationToken ct)
        {
            if (ids == null || ids.Count == 0)
            {
                TempData["err"] = "לא נבחרו מנות להנפקה.";
                return RedirectToAction(nameof(Routine));
            }

            var issued = await _repo.IssueByIdsAsync(ids, "Routine", ct);

            if (issued.Count == 0)
                TempData["err"] = "לא הונפקו מנות (יתכן שכבר הונפקו/לא זמינות).";
            else
                TempData["ok"] = $"הונפקו {issued.Count} מנות: {string.Join(", ", issued.Select(u => u.Id))}";

            return RedirectToAction(nameof(Routine));
        }

        [HttpGet]
        public async Task<IActionResult> Emergency(CancellationToken ct)
        {
            ViewBag.ONegCount = await _repo.CountONegAsync(ct);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmergencyIssue(CancellationToken ct)
        {
            var issued = await _repo.IssueEmergencyONegAsync(ct);
            if (issued.Count == 0)
                TempData["err"] = "אין מלאי O-! דרוש טיפול מיידי.";
            else
                TempData["ok"]  = $"נופקו {issued.Count} מנות O-: {string.Join(", ", issued.Select(u => u.Id))}";
            return RedirectToAction(nameof(Emergency));
        }
    }
}
