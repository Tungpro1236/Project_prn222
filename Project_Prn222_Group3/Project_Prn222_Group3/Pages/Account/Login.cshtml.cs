using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using Project_Prn222_Group3.Services;

namespace Project_Prn222_Group3.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly VcmsDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public LoginModel(VcmsDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(AppRoles.HomePath(User.FindFirstValue(ClaimTypes.Role)));
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập tên đăng nhập và mật khẩu.";
            return Page();
        }

        var username = Username.Trim();
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user is null)
        {
            ErrorMessage = "Tên đăng nhập hoặc mật khẩu không đúng.";
            return Page();
        }

        if (!user.IsActive)
        {
            ErrorMessage = "Tài khoản đã bị khóa. Vui lòng liên hệ quản lý phòng khám.";
            return Page();
        }

        var verifyResult = _hasher.VerifyHashedPassword(user, user.PasswordHash, Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            ErrorMessage = "Tên đăng nhập hoặc mật khẩu không đúng.";
            return Page();
        }

        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, Password);
            await _db.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("FullName", user.FullName),
            new(ClaimTypes.Role, user.Role.RoleName),
            new(ActiveUserCookieEvents.CredentialVersionClaim,
                ActiveUserCookieEvents.CredentialVersion(user.PasswordHash))
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = ClinicTime.UtcNow.AddDays(RememberMe ? 7 : 1),
                AllowRefresh = true
            });

        return LocalRedirect(AppRoles.HomePath(user.Role.RoleName));
    }
}
