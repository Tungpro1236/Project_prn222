using Microsoft.AspNetCore.Authorization;
using Project_Prn222_Group3.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;

namespace Project_Prn222_Group3.Pages.Audit
{
    [Authorize(Roles = AppRoles.Managers)]
    public class IndexModel : PageModel
    {
        private readonly VcmsDbContext _context;

        public IndexModel(VcmsDbContext context)
        {
            _context = context;
        }

        // Pagination
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public int PageSize { get; } = 20;

        public int TotalPages { get; set; } = 1;

        public int TotalRecords { get; set; }

        // Search
        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        // Action filter
        [BindProperty(SupportsGet = true)]
        public string? ActionFilter { get; set; }

        // Date filters
        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }

        public string? ErrorMessage { get; set; }

        // Audit log list
        public List<AuditLogRow> AuditLogs { get; set; } = new();

        public class AuditLogRow
        {
            public long AuditLogID { get; set; }

            public string ActorName { get; set; } = "";

            public string EntityName { get; set; } = "";

            public string EntityID { get; set; } = "";

            public string Action { get; set; } = "";

            public DateTime OccurredAt { get; set; }
            public string? OldValues { get; set; }
            public string? NewValues { get; set; }
            public long? PreviousAuditLogID { get; set; }
        }

        public List<string> Actions { get; set; } = [];

        public async Task OnGetAsync()
        {
            Actions = await _context.AuditLogs.AsNoTracking().Select(x => x.Action).Distinct().OrderBy(x => x).ToListAsync();
            if (!ModelState.IsValid || FromDate?.Year is < 2000 or > 2099 || ToDate?.Year is < 2000 or > 2099)
            {
                ErrorMessage = "Ngày lọc phải hợp lệ, trong khoảng năm 2000–2099."; return;
            }
            // Validate date range
            if (FromDate.HasValue &&
                ToDate.HasValue &&
                FromDate.Value.Date > ToDate.Value.Date)
            {
                ErrorMessage =
                    "Ngày bắt đầu không được sau ngày kết thúc.";

                return;
            }

            // Join AuditLogs with Users
            var query =
                from log in _context.AuditLogs.AsNoTracking()
                join user in _context.Users.AsNoTracking()
                    on log.ActorUserID equals (int?)user.UserID
                    into userGroup
                from user in userGroup.DefaultIfEmpty()
                select new
                {
                    Log = log,

                    ActorName = user == null
                        ? (log.ActorUserID == null
                            ? "Hệ thống"
                            : "Không xác định")
                        : user.FullName
                };

            // Search by actor, entity, entity ID
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string keyword = Search.Trim();

                query = query.Where(x =>
                    x.Log.EntityName.Contains(keyword) ||
                    x.Log.EntityID.Contains(keyword) ||
                    x.ActorName.Contains(keyword));
            }

            // Filter by action
            if (!string.IsNullOrWhiteSpace(ActionFilter))
            {
                query = query.Where(x =>
                    x.Log.Action == ActionFilter);
            }

            // Vietnam time zone: UTC+7
            // AuditLogs.OccurredAt is stored in UTC.

            if (FromDate.HasValue)
            {
                DateTime fromUtc =
                    ClinicTime.VnLocalToUtc(FromDate.Value.Date);

                query = query.Where(x =>
                    x.Log.OccurredAt >= fromUtc);
            }

            if (ToDate.HasValue)
            {
                DateTime toUtcExclusive =
                    ClinicTime.VnLocalToUtc(ToDate.Value.Date.AddDays(1));

                query = query.Where(x =>
                    x.Log.OccurredAt < toUtcExclusive);
            }

            // Count filtered records
            TotalRecords = await query.CountAsync();

            TotalPages = Math.Max(1, (int)Math.Ceiling(
                TotalRecords / (double)PageSize));

            // Validate page number
            if (PageNumber < 1)
            {
                PageNumber = 1;
            }

            if (TotalPages == 0) PageNumber = 1;
            if (TotalPages > 0 && PageNumber > TotalPages)
            {
                PageNumber = TotalPages;
            }

            // Get current page
            AuditLogs = await query
                .OrderByDescending(x => x.Log.OccurredAt)
                .ThenByDescending(x => x.Log.AuditLogID)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .Select(x => new AuditLogRow
                {
                    AuditLogID = x.Log.AuditLogID,

                    ActorName = x.ActorName,

                    EntityName = x.Log.EntityName,

                    EntityID = x.Log.EntityID,

                    Action = x.Log.Action,

                    OccurredAt = x.Log.OccurredAt,
                    OldValues = x.Log.OldValues,
                    NewValues = x.Log.NewValues,
                    PreviousAuditLogID = x.Log.PreviousAuditLogID
                })
                .ToListAsync();
        }
    }
}
