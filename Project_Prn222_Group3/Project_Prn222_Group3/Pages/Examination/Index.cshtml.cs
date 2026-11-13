using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
namespace Project_Prn222_Group3.Pages.Examination;

[Authorize(Roles = AppRoles.ClinicalReaders)]
public class IndexModel(VcmsDbContext db) : PageModel
{
    public IList<Visit> Visits { get; set; } = [];
    public async Task OnGetAsync()
    {
        var actor = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var query = db.Visits.AsNoTracking().Include(x => x.Appointment).ThenInclude(x => x.Animal).ThenInclude(x => x.Client)
            .Include(x => x.Encounter).Where(x => x.Status != VisitStatuses.Cancelled);
        if (!User.IsInRole(AppRoles.Admin)) query = query.Where(x => x.Appointment.VeterinarianID == actor);
        Visits = await query.OrderByDescending(x => x.CheckInAt).Take(200).ToListAsync();
    }
}
