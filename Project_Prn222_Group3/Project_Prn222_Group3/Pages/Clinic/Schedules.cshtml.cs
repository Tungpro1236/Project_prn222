using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Pages.Clinic;

[Authorize(Roles = AppRoles.Managers)]
public class SchedulesModel : PageModel
{
    private static readonly (byte Value, string Name)[] Days =
    [
        (1, "Thứ hai"), (2, "Thứ ba"), (3, "Thứ tư"), (4, "Thứ năm"),
        (5, "Thứ sáu"), (6, "Thứ bảy"), (0, "Chủ nhật")
    ];

    private readonly VcmsDbContext _db;

    public SchedulesModel(VcmsDbContext db)
    {
        _db = db;
    }

    public IList<User> Providers { get; set; } = new List<User>();
    public IList<StaffSchedule> Schedules { get; set; } = new List<StaffSchedule>();
    public IList<BlockedTime> Blocks { get; set; } = new List<BlockedTime>();
    public List<SelectListItem> ProviderOptions { get; set; } = new();
    public List<SelectListItem> DayOptions { get; } = Days
        .Select(d => new SelectListItem(d.Name, d.Value.ToString()))
        .ToList();

    [BindProperty]
    public ScheduleInput Schedule { get; set; } = new();

    [BindProperty]
    public BlockInput Block { get; set; } = new();

    public class ScheduleInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhân viên.")]
        public int UserID { get; set; }

        [Range(0, 6, ErrorMessage = "Ngày không hợp lệ.")]
        public byte DayOfWeek { get; set; } = 1;

        [Required(ErrorMessage = "Vui lòng nhập giờ bắt đầu.")]
        public TimeSpan? StartTime { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giờ kết thúc.")]
        public TimeSpan? EndTime { get; set; }

        public bool IsWorking { get; set; } = true;
    }

    public class BlockInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhân viên.")]
        public int UserID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập thời gian bắt đầu.")]
        public DateTime? StartAt { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập thời gian kết thúc.")]
        public DateTime? EndAt { get; set; }

        [StringLength(200, ErrorMessage = "Lý do tối đa 200 ký tự.")]
        public string? Reason { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostScheduleAsync()
    {
        ModelState.Clear();
        if (!await TryUpdateModelAsync(Schedule, nameof(Schedule)))
        {
            await LoadAsync();
            return Page();
        }

        await LoadAsync();
        if (!ProviderOptions.Any(p => p.Value == Schedule.UserID.ToString()))
        {
            ModelState.AddModelError("Schedule.UserID", "Nhân viên không hợp lệ.");
        }

        if (Schedule.StartTime >= Schedule.EndTime)
        {
            ModelState.AddModelError("Schedule.EndTime", "Giờ kết thúc phải sau giờ bắt đầu.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var row = await _db.StaffSchedules
            .FirstOrDefaultAsync(s => s.UserID == Schedule.UserID && s.DayOfWeek == Schedule.DayOfWeek);
        if (row is null)
        {
            row = new StaffSchedule { UserID = Schedule.UserID, DayOfWeek = Schedule.DayOfWeek };
            _db.StaffSchedules.Add(row);
        }

        row.StartTime = Schedule.StartTime!.Value;
        row.EndTime = Schedule.EndTime!.Value;
        row.IsWorking = Schedule.IsWorking;
        await _db.SaveChangesAsync();

        TempData["Flash"] = "Đã lưu lịch làm việc.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostBlockAsync()
    {
        ModelState.Clear();
        if (!await TryUpdateModelAsync(Block, nameof(Block)))
        {
            await LoadAsync();
            return Page();
        }

        await LoadAsync();
        if (!ProviderOptions.Any(p => p.Value == Block.UserID.ToString()))
        {
            ModelState.AddModelError("Block.UserID", "Nhân viên không hợp lệ.");
        }

        if (Block.StartAt >= Block.EndAt)
        {
            ModelState.AddModelError("Block.EndAt", "Thời gian kết thúc phải sau thời gian bắt đầu.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        _db.BlockedTimes.Add(new BlockedTime
        {
            UserID = Block.UserID,
            StartAt = ClinicTime.VnLocalToUtc(Block.StartAt!.Value),
            EndAt = ClinicTime.VnLocalToUtc(Block.EndAt!.Value),
            Reason = string.IsNullOrWhiteSpace(Block.Reason) ? null : Block.Reason.Trim()
        });
        await _db.SaveChangesAsync();

        TempData["Flash"] = "Đã chặn khung giờ.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteBlockAsync(int id)
    {
        var block = await _db.BlockedTimes.FirstOrDefaultAsync(b => b.BlockedTimeID == id);
        if (block is null)
        {
            return NotFound();
        }

        _db.BlockedTimes.Remove(block);
        await _db.SaveChangesAsync();
        TempData["Flash"] = "Đã xóa khung giờ bị chặn.";
        return RedirectToPage();
    }

    public static string DayName(byte day) =>
        Days.FirstOrDefault(d => d.Value == day).Name ?? day.ToString();

    private async Task LoadAsync()
    {
        Providers = await _db.Users
            .Include(u => u.Role)
            .Where(u => u.IsActive && AppRoles.SchedulableRoleNames.Contains(u.Role.RoleName))
            .OrderBy(u => u.FullName)
            .ToListAsync();
        ProviderOptions = Providers
            .Select(u => new SelectListItem($"{u.FullName} - {AppRoles.DisplayName(u.Role.RoleName)}", u.UserID.ToString()))
            .ToList();

        var ids = Providers.Select(p => p.UserID).ToList();
        Schedules = await _db.StaffSchedules
            .Include(s => s.User)
            .Where(s => ids.Contains(s.UserID))
            .OrderBy(s => s.User.FullName)
            .ThenBy(s => s.DayOfWeek == 0 ? 7 : s.DayOfWeek)
            .ToListAsync();

        var from = ClinicTime.VnLocalToUtc(ClinicTime.VnToday);
        Blocks = await _db.BlockedTimes
            .Include(b => b.User)
            .Where(b => ids.Contains(b.UserID) && b.EndAt >= from)
            .OrderBy(b => b.StartAt)
            .ToListAsync();
    }
}
