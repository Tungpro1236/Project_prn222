using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Pages;

[AllowAnonymous]
public class QueueDisplayModel : PageModel
{
    private readonly VcmsDbContext _db;

    public QueueDisplayModel(VcmsDbContext db)
    {
        _db = db;
    }

    public record QueueToken(int Number, string Status, DateTime? CalledAt);

    public DateTime Today { get; set; }

    public IReadOnlyList<QueueToken> Tokens { get; set; } = Array.Empty<QueueToken>();

    public QueueToken? Current { get; set; }

    public int WaitingCount { get; set; }

    public async Task OnGetAsync()
    {
        Today = ClinicTime.VnToday;

        var rows = await _db.QueueEntries
            .AsNoTracking()
            .Where(q => q.QueueDate == Today)
            .OrderBy(q => q.QueueNumber)
            .Select(q => new { q.QueueNumber, q.Status, q.CalledAt })
            .ToListAsync();

        Tokens = rows
            .Select(r => new QueueToken(r.QueueNumber, r.Status, r.CalledAt))
            .ToList();

        Current = Tokens.FirstOrDefault(t => t.Status == QueueStatuses.Serving)
            ?? Tokens.FirstOrDefault(t => t.Status == QueueStatuses.Called);
        WaitingCount = Tokens.Count(t => t.Status == QueueStatuses.Waiting);
    }

    public static string TokenClass(string status) => status switch
    {
        QueueStatuses.Serving => "is-serving",
        QueueStatuses.Called => "is-called",
        QueueStatuses.Done => "is-done",
        QueueStatuses.Skipped => "is-skipped",
        _ => "is-waiting"
    };
}
