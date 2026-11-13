using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Project_Prn222_Group3.Models;
namespace Project_Prn222_Group3.Pages.Clinic;
[Authorize(Roles = AppRoles.Managers)]
public class ServicesModel : PageModel { public void OnGet() { } }
