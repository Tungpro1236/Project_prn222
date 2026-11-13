using System.ComponentModel.DataAnnotations;
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
public class EditModel(VcmsDbContext db, ExaminationService care) : PageModel
{
    public Visit VisitRecord { get; set; } = null!;
    public IList<Prescription> Prescriptions { get; set; } = [];
    public IList<AuditLog> History { get; set; } = [];
    public bool CanWrite => User.IsInRole(AppRoles.Veterinarian);
    public bool IsFinal => VisitRecord.Encounter is { Status: not "Draft" };
    [BindProperty] public FormInput Input { get; set; } = new();
    public class FormInput
    {
        [StringLength(20000, ErrorMessage = "Triệu chứng tối đa 20.000 ký tự.")] public string? Symptoms { get; set; }
        [StringLength(20000, ErrorMessage = "Kết quả khám tối đa 20.000 ký tự.")] public string? ExaminationNotes { get; set; }
        [StringLength(20000, ErrorMessage = "Chẩn đoán tối đa 20.000 ký tự.")] public string? Diagnosis { get; set; }
        [StringLength(500, ErrorMessage = "Lý do hiệu chỉnh tối đa 500 ký tự.")] public string? CorrectionReason { get; set; }
    }
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<bool> Load(int visitId)
    {
        var visit = await db.Visits.AsNoTracking().Include(x => x.Appointment).ThenInclude(x => x.Animal).ThenInclude(x => x.Client)
            .Include(x => x.Encounter).SingleOrDefaultAsync(x => x.VisitID == visitId);
        if (visit is null || (!User.IsInRole(AppRoles.Admin) && (visit.Appointment.VeterinarianID != Actor || (visit.Encounter is not null && visit.Encounter.VeterinarianID != Actor)))) return false;
        VisitRecord = visit;
        if (visit.Encounter is not null)
        {
            var id = visit.Encounter.EncounterID;
            Prescriptions = await db.Prescriptions.AsNoTracking().Where(x => x.EncounterID == id).OrderByDescending(x => x.PrescriptionID).ToListAsync();
            History = await db.AuditLogs.AsNoTracking().Where(x => x.EntityName == "Encounter" && x.EntityID == id.ToString()).OrderByDescending(x => x.AuditLogID).Take(100).ToListAsync();
        }
        return true;
    }
    public async Task<IActionResult> OnGetAsync(int visitId)
    {
        if (!await Load(visitId)) return NotFound();
        if (VisitRecord.Encounter is { } e) Input = new() { Symptoms = e.Symptoms, ExaminationNotes = e.ExaminationNotes, Diagnosis = e.Diagnosis };
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(int visitId, bool finalize)
    {
        if (!CanWrite) return Forbid();
        if (!await Load(visitId)) return NotFound();
        if (!ModelState.IsValid) return Page();
        var result = await care.SaveEncounterAsync(visitId, Actor, new(Input.Symptoms, Input.ExaminationNotes, Input.Diagnosis), finalize, Input.CorrectionReason);
        if (!result.Ok) { ModelState.AddModelError("", result.Message); return Page(); }
        TempData["Flash"] = result.Message; return RedirectToPage(new { visitId });
    }
    public async Task<IActionResult> OnPostPrescriptionAsync(int visitId)
    {
        if (!CanWrite) return Forbid();
        if (!await Load(visitId) || VisitRecord.Encounter is null) return NotFound();
        var result = await care.CreatePrescriptionAsync(VisitRecord.Encounter.EncounterID, Actor);
        if (result.Ok) return RedirectToPage("Prescription", new { id = result.Id });
        ModelState.AddModelError("", result.Message); return Page();
    }
}
