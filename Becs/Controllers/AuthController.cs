using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

public class AuthController : Controller
{
    private readonly IUserRepository _users;
    private readonly IAuthService _auth;
    private readonly IAuditLogger _audit; // <— swap to IAuditLogger

    public AuthController(IUserRepository users, IAuthService auth, IAuditLogger audit)
    {
        _users = users; _auth = auth; _audit = audit;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    public sealed class LoginVm
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string? ReturnUrl { get; set; }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        var user = await _users.FindByUsernameAsync(vm.Username);
        var ok = user is not null && _auth.Verify(vm.Password, user.PasswordHash);

        // audit (hashed chain)
        await _audit.LogAsync(new AuditEntry
        {
            UserId = user?.Id.ToString(),
            UserName = user?.Username,
            ActorType = "User",
            Action = "Auth.Login",
            DetailsJson = JsonSerializer.Serialize(new { vm.Username }),
            Success = ok
        });

        if (!ok)
        {
            ModelState.AddModelError("", "Invalid username or password.");
            return View(vm);
        }

        await _auth.SignInAsync(HttpContext, user!);
        await _users.SetLastLoginAsync(user!.Id, DateTime.UtcNow);

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);

        return user!.Role switch
        {
            "admin" => RedirectToAction("Index", "Admin"),
            "user" => RedirectToAction("Index", "Inventory"),
            "researcher" => RedirectToAction("Index", "Researcher"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync(new AuditEntry
        {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            UserName = User.Identity?.Name,
            ActorType = "User",
            Action = "Auth.Logout",
            Success = true
        });
        await _auth.SignOutAsync(HttpContext);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult Denied() => View();
}
