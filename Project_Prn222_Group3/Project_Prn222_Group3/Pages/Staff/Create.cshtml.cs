using System.ComponentModel.DataAnnotations;
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
public class CreateModel : PageModel
{
    private readonly VcmsDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public CreateModel(VcmsDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> RoleOptions { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(120, ErrorMessage = "Họ tên tối đa 120 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
        [StringLength(80, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 80 ký tự.")]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Tên đăng nhập chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
        public string Username { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(150, ErrorMessage = "Email tối đa 150 ký tự.")]
        public string? Email { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
        public string? Phone { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn vai trò.")]
        public int RoleID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải từ 8 đến 100 ký tự.")]
        public string Password { get; set; } = string.Empty;

        [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        await LoadRolesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleID == Input.RoleID);
        if (role is null || !AppRoles.StaffRoleNames.Any(n => string.Equals(n, role.RoleName, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Input.RoleID", "Vai trò không hợp lệ.");
        }

        var username = Input.Username.Trim();
        var email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim();
        var phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim();

        if (await _db.Users.AnyAsync(u => u.Username == username))
        {
            ModelState.AddModelError("Input.Username", "Tên đăng nhập đã tồn tại.");
        }

        if (email is not null && await _db.Users.AnyAsync(u => u.Email == email))
        {
            ModelState.AddModelError("Input.Email", "Email đã được sử dụng.");
        }

        if (!ModelState.IsValid)
        {
            await LoadRolesAsync();
            return Page();
        }

        var user = new User
        {
            RoleID = role!.RoleID,
            Username = username,
            FullName = Input.FullName.Trim(),
            Email = email,
            Phone = phone,
            IsActive = true,
            CreatedAt = ClinicTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, Input.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        TempData["Flash"] = $"Đã tạo tài khoản {user.Username}.";
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
