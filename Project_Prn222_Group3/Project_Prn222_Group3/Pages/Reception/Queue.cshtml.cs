using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using Project_Prn222_Group3.Services;

namespace Project_Prn222_Group3.Pages.Reception;

[Authorize(Roles = AppRoles.ReceptionDesk)]
public class QueueModel : PageModel
{
    private readonly VcmsDbContext _db;
    private readonly ReceptionService _reception;

    public QueueModel(VcmsDbContext db, ReceptionService reception)
    {
        _db = db;
        _reception = reception;
    }

    public DateTime Today { get; set; }

    public IList<QueueEntry> Entries { get; set; } = new List<QueueEntry>();

    public QueueEntry? NowServing { get; set; }

    public QueueEntry? NextWaiting { get; set; }

    public int WaitingCount { get; set; }
    public int DoneCount { get; set; }

    public async Task OnGetAsync()
    {
        Today = ClinicTime.VnToday;

        Entries = await _db.QueueEntries
            .AsNoTracking()
            .Include(q => q.Visit)
                .ThenInclude(v => v.Appointment)
                    .ThenInclude(a => a.Animal)
                        .ThenInclude(an => an.Client)
            .Include(q => q.Visit)
                .ThenInclude(v => v.Appointment)
                    .ThenInclude(a => a.Veterinarian)
            .Where(q => q.QueueDate == Today)
            .OrderBy(q => q.QueueNumber)
            .ToListAsync();

        NowServing = Entries.FirstOrDefault(q => q.Status == QueueStatuses.Serving)
            ?? Entries.FirstOrDefault(q => q.Status == QueueStatuses.Called);
        NextWaiting = Entries.FirstOrDefault(q => q.Status == QueueStatuses.Waiting);
        WaitingCount = Entries.Count(q => q.Status == QueueStatuses.Waiting);
        DoneCount = Entries.Count(q => q.Status == QueueStatuses.Done);
    }

    public async Task<IActionResult> OnPostAdvanceAsync(int id, QueueAction action)
    {
        var result = await _reception.AdvanceQueueAsync(id, action);
        if (result.Ok)
        {
            TempData["Flash"] = result.Message;
        }
        else
        {
            TempData["FlashError"] = result.Message;
        }

        return RedirectToPage("Queue");
    }
}
