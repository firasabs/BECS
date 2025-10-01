using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

public interface IAuthService
{
    string HashPassword(string plaintext);
    bool Verify(string plaintext, string hash);
    Task SignInAsync(HttpContext http, UserRecord user);
    Task SignOutAsync(HttpContext http);
}

public class AuthService : IAuthService
{
    public string HashPassword(string plaintext) => BCrypt.Net.BCrypt.HashPassword(plaintext, workFactor: 11);
    public bool Verify(string plaintext, string hash) => BCrypt.Net.BCrypt.Verify(plaintext, hash);

    public async Task SignInAsync(HttpContext http, UserRecord user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(id);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    public Task SignOutAsync(HttpContext http) => http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}