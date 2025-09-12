using Becs.Models;
using Becs.Services;
using Microsoft.AspNetCore.Mvc;

namespace Becs.Controllers;

public class DonationsController : Controller
{
    private readonly InventoryService _svc;
    public DonationsController(InventoryService svc) => _svc = svc;

    public IActionResult Index()
    {
        ViewBag.Units = _svc.AllUnits();
        return View(new DonationInput());
    }

    [HttpPost]
    public IActionResult Create(DonationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.DonorId) || string.IsNullOrWhiteSpace(input.DonorName))
        {
            ModelState.AddModelError("", "יש למלא ת״ז ושם מלא.");
        }
        if (!new[] { "O","A","B","AB" }.Contains(input.ABO?.ToUpperInvariant()))
        {
            ModelState.AddModelError("", "ABO לא תקין.");
        }
        if (input.RhSign != "+" && input.RhSign != "-")
        {
            ModelState.AddModelError("", "Rh לא תקין.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Units = _svc.AllUnits();
            return View("Index", input);
        }

        _svc.AddDonation(input);
        TempData["ok"] = "התרומה נקלטה בהצלחה";
        return RedirectToAction(nameof(Index));
    }
}