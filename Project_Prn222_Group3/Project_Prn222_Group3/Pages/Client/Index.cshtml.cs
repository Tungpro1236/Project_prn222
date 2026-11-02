using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using ClientProfile = Project_Prn222_Group3.Models.Client;

namespace Project_Prn222_Group3.Pages.ClientPortal;

[Authorize(Roles = AppRoles.Client)]
public class IndexModel : PageModel
{
    private readonly VcmsDbContext _db;

    public IndexModel(VcmsDbContext db)
    {
        _db = db;
    }

    public ClientProfile? ClientRecord { get; set; }

    public IList<Appointment> Appointments { get; set; } = new List<Appointment>();

    public async Task OnGetAsync()
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        ClientRecord = await _db.Clients
            .AsNoTracking()
            .Include(c => c.Animals)
            .FirstOrDefaultAsync(c => c.UserID == userId);

        if (ClientRecord is null)
        {
            return;
        }

        Appointments = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Animal)
            .Include(a => a.Veterinarian)
            .Where(a => a.Animal.ClientID == ClientRecord.ClientID)
            .OrderByDescending(a => a.AppointmentTime)
            .Take(20)
            .ToListAsync();
    }
}
