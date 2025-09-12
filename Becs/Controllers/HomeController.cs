using Microsoft.AspNetCore.Mvc;

namespace Becs.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}