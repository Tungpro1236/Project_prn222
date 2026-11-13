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
public class DispenseModel(VcmsDbContext db, PharmacyService pharmacy) : PageModel
{
    public Prescription Record { get; set; } = null!;
    public PrescriptionSnapshot? Snapshot { get; set; }
    public string MedicineName(PrescriptionItem item) => Snapshot?.Lines.FirstOrDefault(x => x.ItemId == item.PrescriptionItemID)?.MedicineName ?? item.Medicine.MedicineName;
    public string MedicineUnit(PrescriptionItem item) => Snapshot?.Lines.FirstOrDefault(x => x.ItemId == item.PrescriptionItemID)?.Unit ?? item.Medicine.Unit;
    public IList<Dispensation> History { get; set; } = [];
    public List<BatchOption> Options { get; set; } = [];
    public Dictionary<int, long> Remaining { get; set; } = [];
    [BindProperty] public Guid RequestKey { get; set; }
    [BindProperty] public List<DispenseLine> Lines { get; set; } = [];
    public record BatchOption(PrescriptionItem Item, MedicineBatch Batch);
    private async Task<bool> Load(int id)
    {
        var row = await db.Prescriptions.AsNoTracking().Include(x => x.Encounter).ThenInclude(x => x.Visit).ThenInclude(x => x.Appointment).ThenInclude(x => x.Animal)
            .Include(x => x.Items).ThenInclude(x => x.Medicine).SingleOrDefaultAsync(x => x.PrescriptionID == id);
        if (row is null) return false;
        Record = row;
        Snapshot = await PrescriptionSnapshots.ReadAsync(db, id);
        History = await db.Dispensations.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Batch).Include(x => x.DispensedBy)
            .Where(x => x.PrescriptionID == id).OrderByDescending(x => x.DispensationID).ToListAsync();
        var medIds = row.Items.Select(x => x.MedicineID).Distinct().ToArray();
        var batches = await db.MedicineBatches.AsNoTracking().Where(x => medIds.Contains(x.MedicineID) && x.ExpiryDate >= ClinicTime.VnToday && x.QuantityOnHand > 0)
            .OrderBy(x => x.ExpiryDate).ThenBy(x => x.BatchID).ToListAsync();
        foreach (var item in row.Items)
        {
            Remaining[item.PrescriptionItemID] = item.Quantity - History.Where(x => x.Status == "Completed").SelectMany(x => x.Items).Where(x => x.PrescriptionItemID == item.PrescriptionItemID).Sum(x => (long)x.Quantity);
            if (Remaining[item.PrescriptionItemID] > 0 && item.Medicine.IsActive)
                Options.AddRange(batches.Where(x => x.MedicineID == item.MedicineID).Select(x => new BatchOption(item, x)));
        }
        return true;
    }
    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await Load(id)) return NotFound();
        RequestKey = Guid.NewGuid(); return Page();
    }
    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!ModelState.IsValid || Lines.Any(x => x.Quantity < 0))
        {
            if (!await Load(id)) return NotFound();
            ModelState.AddModelError("", "Kiểm tra các số lượng cần cấp."); return Page();
        }
        var actor = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await pharmacy.DispenseAsync(id, actor, RequestKey, Lines.Where(x => x.Quantity > 0).ToArray());
        if (!result.Ok)
        {
            if (!await Load(id)) return NotFound();
            ModelState.AddModelError("", result.Message); return Page();
        }
        TempData["Flash"] = result.Message; return RedirectToPage(new { id });
    }
}
