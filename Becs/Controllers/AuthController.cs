using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

public class AuthController : Controller
{
    private readonly IUserRepository _users;
    private readonly IAuthService _auth;     // keep for SignIn/SignOut only (no bcrypt)
    private readonly IAuditLogger _audit;

    public AuthController(IUserRepository users, IAuthService auth, IAuditLogger audit)
    {
        _users = users; _auth = auth; _audit = audit;
    }

    [HttpGet, AllowAnonymous]

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

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        var user = await _users.FindByUsernameAsync(vm.Username)  ;

        // NEW: SHA-256 + salt verification
        var ok = user is not null && VerifyPassword(vm.Password, user.PasswordHash?.Trim() ?? "");
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
            "admin"      => RedirectToAction("Index", "Admin"),
            "user"       => RedirectToAction("Index", "Home"),
            "researcher" => RedirectToAction("Index", "Researcher"),
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

    [HttpGet, AllowAnonymous]
    public IActionResult Denied() => View();

    // ------------ helpers --------------

    // storedPasswordHash must be "<hashBase64>:<saltBase64>"
    private static bool VerifyPassword(string enteredPassword, string storedPasswordHash)
    {
        var parts = storedPasswordHash.Split(':');
        if (parts.Length != 2) return false;

        var storedHash = parts[0];
        var storedSalt = parts[1];

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(enteredPassword + storedSalt);
        var computed = sha256.ComputeHash(bytes);
        var computedBase64 = Convert.ToBase64String(computed);

        return computedBase64 == storedHash;
    }
}
