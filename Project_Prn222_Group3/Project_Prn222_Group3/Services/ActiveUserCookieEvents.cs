using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;

namespace Project_Prn222_Group3.Services;

// Check every authenticated request, including reads and antiforgery-protected POSTs.
// A password fingerprint supports reset detection without adding a schema column
// or copying the password hash into the protected authentication ticket.
public sealed class ActiveUserCookieEvents(VcmsDbContext db) : CookieAuthenticationEvents
{
    public const string CredentialVersionClaim = "Vcms.CredentialVersion";

    public static string CredentialVersion(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var user = int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? await db.Users.AsNoTracking().Include(x => x.Role)
                .SingleOrDefaultAsync(x => x.UserID == id, context.HttpContext.RequestAborted)
            : null;

        if (user is null || !user.IsActive
            || principal?.FindFirstValue(ClaimTypes.Role) != user.Role.RoleName
            || principal.Identity?.Name != user.Username
            || principal.FindFirstValue(CredentialVersionClaim) != CredentialVersion(user.PasswordHash))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
        }
    }
}
