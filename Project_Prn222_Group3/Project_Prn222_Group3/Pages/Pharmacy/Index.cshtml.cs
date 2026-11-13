using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
namespace Project_Prn222_Group3.Pages.Pharmacy;
[Authorize(Roles = AppRoles.PharmacyStaff)]
public class IndexModel(VcmsDbContext db) : PageModel
{
    public IList<Medicine> Medicines { get; set; } = [];
    public IList<Prescription> Prescriptions { get; set; } = [];
    public async Task OnGetAsync()
    {
        Medicines = await db.Medicines.AsNoTracking().Include(x => x.Batches).OrderBy(x => x.MedicineName).ToListAsync();
        Prescriptions = await db.Prescriptions.AsNoTracking().Include(x => x.Encounter).ThenInclude(x => x.Visit).ThenInclude(x => x.Appointment).ThenInclude(x => x.Animal)
            .Where(x => x.Status == "Finalized").OrderByDescending(x => x.FinalizedAt).Take(100).ToListAsync();
    }
}
