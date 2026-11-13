using Microsoft.AspNetCore.Authorization;
using Project_Prn222_Group3.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;


namespace Project_Prn222_Group3.Pages.Audit
{
    [Authorize(Roles = AppRoles.Managers)]
    public class DetailsModel : PageModel
    {
        private readonly VcmsDbContext _context;

        public DetailsModel(VcmsDbContext context)
        {
            _context = context;
        }

        public AuditLog? AuditLog { get; set; }

        public string ActorName { get; set; } = "";

        public async Task<IActionResult> OnGetAsync(long id)
        {
            AuditLog = await _context.AuditLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AuditLogID == id);

            if (AuditLog == null)
                return NotFound();

            if (AuditLog.ActorUserID == null)
            {
                ActorName = "Hệ thống";
            }
            else
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.UserID == AuditLog.ActorUserID);

                ActorName = user?.FullName ?? "Không xác định";
            }

            return Page();
        }
    }
}
