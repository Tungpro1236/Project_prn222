using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using Project_Prn222_Group3.Services;
namespace Project_Prn222_Group3.Pages.Pharmacy;
[Authorize(Roles = AppRoles.PharmacyStaff)]
public class MedicineModel(VcmsDbContext db, PharmacyService pharmacy) : PageModel
{
    [BindProperty] public FormInput Input { get; set; } = new();
    public class FormInput
    {
        [Required(ErrorMessage = "Vui lòng nhập tên thuốc."), StringLength(150, ErrorMessage = "Tên thuốc tối đa 150 ký tự.")] public string MedicineName { get; set; } = "";
        [Required(ErrorMessage = "Vui lòng nhập đơn vị."), StringLength(30, ErrorMessage = "Đơn vị tối đa 30 ký tự.")] public string Unit { get; set; } = "";
        [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Giá phải từ 0 đến 1.000.000.000 VND.")] public decimal CurrentPrice { get; set; }
        public bool IsActive { get; set; } = true;
    }
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null) return Page();
        var row = await db.Medicines.AsNoTracking().SingleOrDefaultAsync(x => x.MedicineID == id);
        if (row is null) return NotFound();
        Input = new() { MedicineName = row.MedicineName, Unit = row.Unit, CurrentPrice = row.CurrentPrice, IsActive = row.IsActive };
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (!ModelState.IsValid) return Page();
        var actor = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await pharmacy.SaveMedicineAsync(id, actor, new(Input.MedicineName, Input.Unit, Input.CurrentPrice, Input.IsActive));
        if (!result.Ok) { ModelState.AddModelError("", result.Message); return Page(); }
        TempData["Flash"] = result.Message; return RedirectToPage("Index");
    }
}
