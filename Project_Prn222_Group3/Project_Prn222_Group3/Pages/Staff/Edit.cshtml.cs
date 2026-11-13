using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Pages.Staff;

[Authorize(Roles = AppRoles.Managers)]
public class EditModel : PageModel
{
    private readonly VcmsDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public EditModel(VcmsDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> RoleOptions { get; set; } = new();

    public string TargetUsername { get; set; } = string.Empty;

    public bool IsSelf { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(120, ErrorMessage = "Họ tên tối đa 120 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(150, ErrorMessage = "Email tối đa 150 ký tự.")]
        public string? Email { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
        public string? Phone { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn vai trò.")]
        public int RoleID { get; set; }

        public bool IsActive { get; set; }

        public string? NewPassword { get; set; }

        public string? ConfirmNewPassword { get; set; }
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var target = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == id);
        if (target is null || target.Role.RoleName == AppRoles.Client)
        {
            return NotFound();
        }

        TargetUsername = target.Username;
        IsSelf = target.UserID == CurrentUserId;
        Input = new InputModel
        {
            FullName = target.FullName,
            Email = target.Email,
            Phone = target.Phone,
            RoleID = target.RoleID,
            IsActive = target.IsActive
        };

        await LoadRolesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var target = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == id);
        if (target is null || target.Role.RoleName == AppRoles.Client)
        {
            return NotFound();
        }

        TargetUsername = target.Username;
        IsSelf = target.UserID == CurrentUserId;

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleID == Input.RoleID);
        if (role is null || !AppRoles.StaffRoleNames.Any(n => string.Equals(n, role.RoleName, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Input.RoleID", "Vai trò không hợp lệ.");
        }

        if (IsSelf && role is not null && role.RoleID != target.RoleID)
        {
            ModelState.AddModelError("Input.RoleID", "Bạn không thể tự thay đổi vai trò của mình.");
        }

        if (IsSelf && !Input.IsActive)
        {
            ModelState.AddModelError("Input.IsActive", "Bạn không thể tự khóa tài khoản đang đăng nhập.");
        }

        var willRemainActiveManager =
            Input.IsActive && role is not null &&
            string.Equals(role.RoleName, AppRoles.ClinicManager, StringComparison.OrdinalIgnoreCase);
        var isActiveManager =
            target.IsActive &&
            string.Equals(target.Role.RoleName, AppRoles.ClinicManager, StringComparison.OrdinalIgnoreCase);

        if (isActiveManager && !willRemainActiveManager)
        {
            var otherManagers = await _db.Users.CountAsync(u =>
                u.UserID != target.UserID && u.IsActive && u.Role.RoleName == AppRoles.ClinicManager);
            if (otherManagers == 0)
            {
                ModelState.AddModelError(string.Empty, "Phải còn ít nhất một quản lý phòng khám đang hoạt động.");
            }
        }

        var email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim();
        if (email is not null && await _db.Users.AnyAsync(u => u.UserID != target.UserID && u.Email == email))
        {
            ModelState.AddModelError("Input.Email", "Email đã được sử dụng.");
        }

        var changePassword = !string.IsNullOrWhiteSpace(Input.NewPassword);
        if (changePassword)
        {
            if (Input.NewPassword!.Length < 8 || Input.NewPassword.Length > 100)
            {
                ModelState.AddModelError("Input.NewPassword", "Mật khẩu mới phải từ 8 đến 100 ký tự.");
            }

            if (Input.NewPassword != Input.ConfirmNewPassword)
            {
                ModelState.AddModelError("Input.ConfirmNewPassword", "Mật khẩu xác nhận không khớp.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadRolesAsync();
            return Page();
        }

        target.FullName = Input.FullName.Trim();
        target.Email = email;
        target.Phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim();
        target.RoleID = role!.RoleID;
        target.Role = role;
        target.IsActive = Input.IsActive;

        if (changePassword)
        {
            target.PasswordHash = _hasher.HashPassword(target, Input.NewPassword!);
        }

        await _db.SaveChangesAsync();

        TempData["Flash"] = $"Đã cập nhật tài khoản {target.Username}.";
        return RedirectToPage("Index");
    }

    private async Task LoadRolesAsync()
    {
        var roles = await _db.Roles.OrderBy(r => r.RoleName).ToListAsync();
        RoleOptions = roles
            .Where(r => AppRoles.StaffRoleNames.Any(n => string.Equals(n, r.RoleName, StringComparison.OrdinalIgnoreCase)))
            .Select(r => new SelectListItem(AppRoles.DisplayName(r.RoleName), r.RoleID.ToString()))
            .ToList();
    }
}
