using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
using Project_Prn222_Group3.Services;
namespace Project_Prn222_Group3.Pages.Examination;

[Authorize(Roles = AppRoles.ClinicalReaders)]
public class PrescriptionModel(VcmsDbContext db, ExaminationService care) : PageModel
{
    public Prescription Record { get; set; } = null!;
    public PrescriptionSnapshot? Snapshot { get; set; }
    public string MedicineName(PrescriptionItem item) => Snapshot?.Lines.FirstOrDefault(x => x.ItemId == item.PrescriptionItemID)?.MedicineName ?? item.Medicine.MedicineName;
    public string MedicineUnit(PrescriptionItem item) => Snapshot?.Lines.FirstOrDefault(x => x.ItemId == item.PrescriptionItemID)?.Unit ?? item.Medicine.Unit;
    public IList<Medicine> Medicines { get; set; } = [];
    public IList<AuditLog> History { get; set; } = [];
    public bool CanWrite => User.IsInRole(AppRoles.Veterinarian);
    public bool CanEdit => CanWrite && Record.Status == "Draft";
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<bool> Load(int id)
    {
        var row = await db.Prescriptions.AsNoTracking().Include(x => x.Encounter).ThenInclude(x => x.Visit).ThenInclude(x => x.Appointment).ThenInclude(x => x.Animal)
            .Include(x => x.Items).ThenInclude(x => x.Medicine).SingleOrDefaultAsync(x => x.PrescriptionID == id);
        if (row is null || (!User.IsInRole(AppRoles.Admin) && (row.PrescribedByUserID != Actor || row.Encounter.VeterinarianID != Actor || row.Encounter.Visit.Appointment.VeterinarianID != Actor))) return false;
        Record = row;
        Snapshot = await PrescriptionSnapshots.ReadAsync(db, id);
        Medicines = await db.Medicines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.MedicineName).ToListAsync();
        History = await db.AuditLogs.AsNoTracking().Where(x => x.EntityName == "Prescription" && x.EntityID == id.ToString()).OrderByDescending(x => x.AuditLogID).Take(100).ToListAsync();
        return true;
    }
    public async Task<IActionResult> OnGetAsync(int id) => await Load(id) ? Page() : NotFound();
    private async Task<IActionResult> Finish(int id, OperationResult result)
    {
        if (result.Ok) { TempData["Flash"] = result.Message; return RedirectToPage(new { id }); }
        if (!await Load(id)) return NotFound();
        ModelState.AddModelError("", result.Message); return Page();
    }
    public async Task<IActionResult> OnPostItemAsync(int id, int? itemId, int medicineId, int quantity, string? dosage, string? frequency, int? durationDays, string? instructions)
    {
        if (!CanWrite) return Forbid();
        if (!ModelState.IsValid) return await Finish(id, OperationResult.Fail("Kiểm tra số lượng và số ngày dùng thuốc."));
        return await Finish(id, await care.SaveItemAsync(id, itemId, Actor, new(medicineId, quantity, dosage ?? "", frequency, durationDays, instructions)));
    }
    public async Task<IActionResult> OnPostDeleteAsync(int id, int itemId)
    {
        if (!CanWrite) return Forbid();
        return await Finish(id, await care.SaveItemAsync(id, itemId, Actor, new(0, 0, "", null, null, null), delete: true));
    }
    public async Task<IActionResult> OnPostFinalizeAsync(int id, string? instructions)
    {
        if (!CanWrite) return Forbid();
        return await Finish(id, await care.FinalizePrescriptionAsync(id, Actor, instructions));
    }
    public async Task<IActionResult> OnPostCancelAsync(int id, string? reason)
    {
        if (!CanWrite) return Forbid();
        return await Finish(id, await care.CancelPrescriptionAsync(id, Actor, reason ?? ""));
    }
}
