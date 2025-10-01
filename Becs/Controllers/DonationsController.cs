// Controllers/DonationsController.cs
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

    public class DonationsController : Controller
    {
        private readonly IIntakeRepository _repo;
        public DonationsController(IIntakeRepository repo) => _repo = repo;

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewBag.Units = await _repo.GetUnitsAsync(ct);
            return View(new DonationInput { DonationDate = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DonationInput input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.DonorId) || string.IsNullOrWhiteSpace(input.DonorName))
                ModelState.AddModelError("", "יש למלא ת״ז ושם מלא.");
            if (!new[] { "O","A","B","AB" }.Contains(input.ABO?.ToUpperInvariant()))
                ModelState.AddModelError("", "ABO לא תקין.");
            if (input.RhSign != "+" && input.RhSign != "-")
                ModelState.AddModelError("", "Rh לא תקין.");

            if (!ModelState.IsValid)
            {
                ViewBag.Units = await _repo.GetUnitsAsync(ct);
                return View("Index", input);
            }

            await _repo.InsertDonationAsync(input, ct);
            TempData["ok"] = "התרומה נקלטה בהצלחה";
            return RedirectToAction(nameof(Index));
        }
    }
}