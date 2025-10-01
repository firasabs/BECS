using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[Authorize(Policy="AdminOnly")]
public class AdminController : Controller
{
    private readonly IUserRepository _users;
    private readonly IAuthService _auth;
    private readonly IAuditLogger _audit; // <— use IAuditLogger

    public AdminController(IUserRepository users, IAuthService auth, IAuditLogger audit)
    {
        _users = users; _auth = auth; _audit = audit;
    }

    public IActionResult Index() => View();

    public class CreateUserVm
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "user"; // admin|user|researcher
    }

    [HttpGet]
    public IActionResult CreateUser() => View(new CreateUserVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserVm vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Username) || string.IsNullOrWhiteSpace(vm.Password))
            ModelState.AddModelError("", "Username and password are required.");

        if (!new[] { "admin", "user", "researcher" }.Contains(vm.Role))
            ModelState.AddModelError("", "Invalid role.");

        if (!ModelState.IsValid) return View(vm);

        if (await _users.UsernameExistsAsync(vm.Username))
        {
            ModelState.AddModelError("", "Username already exists.");
            return View(vm);
        }

        var hash = _auth.HashPassword(vm.Password);
        var rows = await _users.CreateAsync(vm.Username, hash, vm.Role);

        await _audit.LogAsync(new AuditEntry
        {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            UserName = User.Identity?.Name,
            ActorType = "User",
            Action = "Admin.CreateUser",
            DetailsJson = JsonSerializer.Serialize(new { vm.Username, vm.Role }),
            Success = rows > 0
        });

        TempData["ok"] = $"User '{vm.Username}' created with role '{vm.Role}'.";
        return RedirectToAction("Index");
    }
}
