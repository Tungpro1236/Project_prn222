using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Pages;

public class IndexModel : PageModel
{
    public string FullName => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? string.Empty;

    public string RoleName => User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    public bool CanManageAccounts =>
        User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.ClinicManager);

    public bool CanWorkAtReception =>
        User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Receptionist);

    public bool CanEditRecords =>
        User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Receptionist) || User.IsInRole(AppRoles.ClinicManager);

    public bool CanBook =>
        CanEditRecords || User.IsInRole(AppRoles.Client);

    public bool CanViewAppointments =>
        CanBook || User.IsInRole(AppRoles.Veterinarian) || User.IsInRole(AppRoles.PetGroomer);

    public bool IsClient => User.IsInRole(AppRoles.Client);

    public void OnGet()
    {
    }
}
