using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Pages.Staff;

[Authorize(Roles = AppRoles.Managers)]
public class IndexModel : PageModel
{
    private readonly VcmsDbContext _db;

    public IndexModel(VcmsDbContext db)
    {
        _db = db;
    }

    public IList<User> Staff { get; set; } = new List<User>();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task OnGetAsync()
    {
        var query = _db.Users
            .Include(u => u.Role)
            .Where(u => u.Role.RoleName != AppRoles.Client);

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var keyword = Q.Trim();
            query = query.Where(u =>
                u.FullName.Contains(keyword) ||
                u.Username.Contains(keyword) ||
                (u.Email != null && u.Email.Contains(keyword)) ||
                (u.Phone != null && u.Phone.Contains(keyword)));
        }

        Staff = await query.OrderBy(u => u.FullName).ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, string? q)
    {
        var target = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == id);
        if (target is null || target.Role.RoleName == AppRoles.Client)
        {
            return NotFound();
        }

        if (target.UserID == CurrentUserId)
        {
            TempData["FlashError"] = "Bạn không thể tự khóa tài khoản đang đăng nhập.";
            return RedirectToPage("Index", new { q });
        }

        if (target.IsActive && target.Role.RoleName == AppRoles.ClinicManager)
        {
            var otherManagers = await _db.Users.CountAsync(u =>
                u.UserID != target.UserID && u.IsActive && u.Role.RoleName == AppRoles.ClinicManager);
            if (otherManagers == 0)
            {
                TempData["FlashError"] = "Phải còn ít nhất một quản lý phòng khám đang hoạt động.";
                return RedirectToPage("Index", new { q });
            }
        }

        target.IsActive = !target.IsActive;
        await _db.SaveChangesAsync();

        TempData["Flash"] = target.IsActive
            ? $"Đã mở khóa tài khoản {target.Username}."
            : $"Đã khóa tài khoản {target.Username}.";
        return RedirectToPage("Index", new { q });
    }
}
