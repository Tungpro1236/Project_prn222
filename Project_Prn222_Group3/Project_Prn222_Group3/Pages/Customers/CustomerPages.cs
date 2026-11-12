using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using ClientProfile = Project_Prn222_Group3.Models.Client;

namespace Project_Prn222_Group3.Pages.Customers;

[Authorize(Roles = AppRoles.RecordEditors)]
public class IndexModel : PageModel
{
    private readonly VcmsDbContext _db;

    public IndexModel(VcmsDbContext db)
    {
        _db = db;
    }

    public IList<ClientProfile> Clients { get; set; } = new List<ClientProfile>();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public async Task OnGetAsync()
    {
        var query = _db.Clients.Include(c => c.Animals).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var keyword = Q.Trim();
            query = query.Where(c =>
                c.FullName.Contains(keyword) ||
                c.Phone.Contains(keyword) ||
                (c.Email != null && c.Email.Contains(keyword)) ||
                c.Animals.Any(a => a.Name.Contains(keyword)));
        }

        Clients = await query.OrderBy(c => c.FullName).Take(100).ToListAsync();
    }
}

[Authorize(Roles = AppRoles.RecordEditors)]
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
    public ClientInput Input { get; set; } = new();

    public class ClientInput
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [RegularExpression(@"^0\d{8,10}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng 0 và có 9-11 số.")]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(150)]
        public string? Email { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        public bool CreateLogin { get; set; } = true;

        [StringLength(80, MinimumLength = 3)]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Tên đăng nhập chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
        public string? Username { get; set; }

        [StringLength(100, MinimumLength = 8)]
        public string? Password { get; set; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var phone = Input.Phone.Trim();
        var email = Clean(Input.Email);
        if (await _db.Clients.AnyAsync(c => c.Phone == phone))
        {
            ModelState.AddModelError("Input.Phone", "Số điện thoại đã có trong hồ sơ khách hàng.");
        }

        User? login = null;
        if (!Input.CreateLogin)
        {
            ModelState.Remove("Input.Username");
            ModelState.Remove("Input.Password");
        }
        else if (string.IsNullOrWhiteSpace(Input.Username) || string.IsNullOrWhiteSpace(Input.Password))
        {
            ModelState.AddModelError("Input.Username", "Cần tên đăng nhập và mật khẩu để tạo tài khoản khách.");
        }
        else if (await _db.Users.AnyAsync(u => u.Username == Input.Username.Trim()))
        {
            ModelState.AddModelError("Input.Username", "Tên đăng nhập đã tồn tại.");
        }
        else if (email is not null && await _db.Users.AnyAsync(u => u.Email == email))
        {
            ModelState.AddModelError("Input.Email", "Email đã được dùng cho tài khoản khác.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Input.CreateLogin)
        {
            var role = await _db.Roles.FirstAsync(r => r.RoleName == AppRoles.Client);
            login = new User
            {
                RoleID = role.RoleID,
                Username = Input.Username!.Trim(),
                FullName = Input.FullName.Trim(),
                Email = email,
                Phone = phone,
                IsActive = true,
                CreatedAt = ClinicTime.UtcNow
            };
            login.PasswordHash = _hasher.HashPassword(login, Input.Password!);
            _db.Users.Add(login);
        }

        var client = new ClientProfile
        {
            User = login,
            FullName = Input.FullName.Trim(),
            Phone = phone,
            Email = email,
            Address = Clean(Input.Address),
            CreatedAt = ClinicTime.UtcNow
        };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        TempData["Flash"] = $"Đã tạo hồ sơ {client.FullName}.";
        return RedirectToPage("Details", new { id = client.ClientID });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

[Authorize(Roles = AppRoles.RecordEditors)]
public class EditModel : PageModel
{
    private readonly VcmsDbContext _db;

    public EditModel(VcmsDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public CreateModel.ClientInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.ClientID == id);
        if (client is null)
        {
            return NotFound();
        }

        Input = new CreateModel.ClientInput
        {
            FullName = client.FullName,
            Phone = client.Phone,
            Email = client.Email,
            Address = client.Address,
            CreateLogin = false
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        ModelState.Remove("Input.Username");
        ModelState.Remove("Input.Password");

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.ClientID == id);
        if (client is null)
        {
            return NotFound();
        }

        var phone = Input.Phone.Trim();
        if (await _db.Clients.AnyAsync(c => c.ClientID != id && c.Phone == phone))
        {
            ModelState.AddModelError("Input.Phone", "Số điện thoại đã có trong hồ sơ khách hàng.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        client.FullName = Input.FullName.Trim();
        client.Phone = phone;
        client.Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim();
        client.Address = string.IsNullOrWhiteSpace(Input.Address) ? null : Input.Address.Trim();
        await _db.SaveChangesAsync();

        TempData["Flash"] = "Đã cập nhật hồ sơ khách hàng.";
        return RedirectToPage("Details", new { id });
    }
}
