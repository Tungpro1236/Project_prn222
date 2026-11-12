using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Pages.Clinic;

[Authorize(Roles = AppRoles.Managers)]
public class SettingsModel : PageModel
{
    private readonly VcmsDbContext _db;

    public SettingsModel(VcmsDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên phòng khám.")]
        [StringLength(120, ErrorMessage = "Tên phòng khám tối đa 120 ký tự.")]
        public string ClinicName { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự.")]
        public string? Address { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(150, ErrorMessage = "Email tối đa 150 ký tự.")]
        public string? Email { get; set; }

        [Range(5, 120, ErrorMessage = "Mỗi khung giờ phải từ 5 đến 120 phút.")]
        public int SlotMinutes { get; set; } = 30;

        [Range(0, 23, ErrorMessage = "Giờ mở cửa không hợp lệ.")]
        public int OpenHour { get; set; } = 8;

        [Range(1, 24, ErrorMessage = "Giờ đóng cửa không hợp lệ.")]
        public int CloseHour { get; set; } = 17;
    }

    public async Task OnGetAsync()
    {
        var setting = await _db.ClinicSettings.OrderBy(s => s.ClinicSettingID).FirstOrDefaultAsync();
        if (setting is null)
        {
            return;
        }

        Input = new InputModel
        {
            ClinicName = setting.ClinicName,
            Address = setting.Address,
            Phone = setting.Phone,
            Email = setting.Email,
            SlotMinutes = setting.SlotMinutes,
            OpenHour = setting.OpenHour,
            CloseHour = setting.CloseHour
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.CloseHour <= Input.OpenHour)
        {
            ModelState.AddModelError("Input.CloseHour", "Giờ đóng cửa phải sau giờ mở cửa.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var setting = await _db.ClinicSettings.OrderBy(s => s.ClinicSettingID).FirstOrDefaultAsync();
        if (setting is null)
        {
            setting = new ClinicSetting();
            _db.ClinicSettings.Add(setting);
        }

        setting.ClinicName = Input.ClinicName.Trim();
        setting.Address = Clean(Input.Address);
        setting.Phone = Clean(Input.Phone);
        setting.Email = Clean(Input.Email);
        setting.SlotMinutes = Input.SlotMinutes;
        setting.OpenHour = Input.OpenHour;
        setting.CloseHour = Input.CloseHour;
        setting.UpdatedAt = ClinicTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Flash"] = "Đã lưu thông tin phòng khám.";
        return RedirectToPage();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
