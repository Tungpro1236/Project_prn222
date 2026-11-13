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
public class ServicesModel : PageModel
{
    private readonly VcmsDbContext _db;

    public ServicesModel(VcmsDbContext db)
    {
        _db = db;
    }

    public IList<ClinicService> Services { get; set; } = new List<ClinicService>();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> CategoryOptions { get; } = ServiceCategories.All
        .Select(c => new SelectListItem(ServiceCategories.Display(c), c))
        .ToList();

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ.")]
        [StringLength(120, ErrorMessage = "Tên dịch vụ tối đa 120 ký tự.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn nhóm dịch vụ.")]
        public string Category { get; set; } = ServiceCategories.Veterinary;

        [Range(5, 480, ErrorMessage = "Thời lượng phải từ 5 đến 480 phút.")]
        public int DurationMinutes { get; set; } = 30;

        [Range(0, 100000000, ErrorMessage = "Giá không hợp lệ.")]
        public decimal Price { get; set; }

        [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
        public string? Description { get; set; }
    }

    public async Task OnGetAsync()
    {
        Services = await _db.Services.OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ServiceCategories.IsValid(Input.Category))
        {
            ModelState.AddModelError("Input.Category", "Nhóm dịch vụ không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        _db.Services.Add(new ClinicService
        {
            Name = Input.Name.Trim(),
            Category = Input.Category,
            DurationMinutes = Input.DurationMinutes,
            Price = Input.Price,
            Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
            IsActive = true
        });
        await _db.SaveChangesAsync();

        TempData["Flash"] = "Đã thêm dịch vụ.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var service = await _db.Services.FirstOrDefaultAsync(s => s.ServiceID == id);
        if (service is null)
        {
            return NotFound();
        }

        service.IsActive = !service.IsActive;
        await _db.SaveChangesAsync();
        TempData["Flash"] = service.IsActive
            ? $"Đã mở lại dịch vụ {service.Name}."
            : $"Đã ngừng dịch vụ {service.Name}.";
        return RedirectToPage();
    }
}
