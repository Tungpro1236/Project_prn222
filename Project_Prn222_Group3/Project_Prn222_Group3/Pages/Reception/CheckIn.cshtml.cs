using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using Project_Prn222_Group3.Services;

namespace Project_Prn222_Group3.Pages.Reception;

[Authorize(Roles = AppRoles.ReceptionDesk)]
public class CheckInModel : PageModel
{
    private readonly VcmsDbContext _db;
    private readonly ReceptionService _reception;

    public CheckInModel(VcmsDbContext db, ReceptionService reception)
    {
        _db = db;
        _reception = reception;
    }

    public DateTime Today { get; set; }

    public IList<Appointment> Appointments { get; set; } = new List<Appointment>();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public int WaitingCount { get; set; }
    public int ServingCount { get; set; }
    public int DoneCount { get; set; }
    public int LastNumber { get; set; }

    public async Task OnGetAsync()
    {
        Today = ClinicTime.VnToday;
        var dayStart = ClinicTime.VnLocalToUtc(Today);
        var dayEnd = ClinicTime.VnLocalToUtc(Today.AddDays(1));

        var query = _db.Appointments
            .AsNoTracking()
            .Include(a => a.Animal)
                .ThenInclude(an => an.Client)
            .Include(a => a.Veterinarian)
            .Include(a => a.Visit)
                .ThenInclude(v => v!.QueueEntry)
            .Where(a => a.AppointmentTime >= dayStart && a.AppointmentTime < dayEnd);

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var keyword = Q.Trim();
            var hasId = int.TryParse(keyword, out var appointmentId);

            query = query.Where(a =>
                a.Animal.Client.FullName.Contains(keyword) ||
                a.Animal.Client.Phone.Contains(keyword) ||
                a.Animal.Name.Contains(keyword) ||
                (hasId && a.AppointmentID == appointmentId));
        }

        Appointments = await query
            .OrderBy(a => a.AppointmentTime)
            .ToListAsync();

        var queue = await _db.QueueEntries
            .AsNoTracking()
            .Where(q => q.QueueDate == Today)
            .ToListAsync();

        WaitingCount = queue.Count(q => q.Status == QueueStatuses.Waiting);
        ServingCount = queue.Count(q => q.Status is QueueStatuses.Called or QueueStatuses.Serving);
        DoneCount = queue.Count(q => q.Status == QueueStatuses.Done);
        LastNumber = queue.Count == 0 ? 0 : queue.Max(q => q.QueueNumber);
    }

    public async Task<IActionResult> OnPostCheckInAsync(int appointmentId)
    {
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        var result = await _reception.CheckInAsync(appointmentId, userId);

        if (result.Ok)
        {
            TempData["Flash"] = result.Message;
            return RedirectToPage("Queue");
        }

        TempData["FlashError"] = result.Message;
        return RedirectToPage("CheckIn");
    }
}
