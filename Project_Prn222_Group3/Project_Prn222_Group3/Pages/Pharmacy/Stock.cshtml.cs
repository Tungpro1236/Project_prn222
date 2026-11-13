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
public class StockModel(VcmsDbContext db, PharmacyService pharmacy) : PageModel
{
    public IList<Medicine> Medicines { get; set; } = [];
    public IList<MedicineBatch> Batches { get; set; } = [];
    public IList<StockMovement> Movements { get; set; } = [];
    public async Task OnGetAsync() => await Load();
    private async Task Load()
    {
        Medicines = await db.Medicines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.MedicineName).ToListAsync();
        Batches = await db.MedicineBatches.AsNoTracking().Include(x => x.Medicine).OrderBy(x => x.ExpiryDate).ThenBy(x => x.BatchID).ToListAsync();
        Movements = await db.StockMovements.AsNoTracking().Include(x => x.Batch).ThenInclude(x => x.Medicine).Include(x => x.PerformedBy).OrderByDescending(x => x.StockMovementID).Take(200).ToListAsync();
    }
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IActionResult> Finish(OperationResult result)
    {
        if (result.Ok) { TempData["Flash"] = result.Message; return RedirectToPage(); }
        ModelState.AddModelError("", result.Message); await Load(); return Page();
    }
    public async Task<IActionResult> OnPostReceiveAsync(int medicineId, string? batchNumber, DateTime expiry, int quantity, decimal? cost, string? reason)
    {
        if (!ModelState.IsValid) return await Finish(OperationResult.Fail("Kiểm tra hạn dùng, số lượng và giá nhập."));
        return await Finish(await pharmacy.ReceiveAsync(medicineId, Actor, batchNumber ?? "", expiry, quantity, cost, reason));
    }
    public async Task<IActionResult> OnPostAdjustAsync(int batchId, int delta, string? reason)
    {
        if (!ModelState.IsValid) return await Finish(OperationResult.Fail("Nhập mức điều chỉnh là số nguyên."));
        return await Finish(await pharmacy.AdjustAsync(batchId, Actor, delta, reason ?? ""));
    }
}
