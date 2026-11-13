using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Services;

public record EncounterInput(string? Symptoms, string? ExaminationNotes, string? Diagnosis);
public record PrescriptionLineInput(int MedicineId, int Quantity, string Dosage, string? Frequency, int? DurationDays, string? Instructions);
public record MedicineSnapshot(int ItemId, int MedicineId, string MedicineName, string Unit, decimal UnitPrice, int Quantity);
public record PrescriptionSnapshot(int PrescriptionId, string? Instructions, MedicineSnapshot[] Lines);

public class ExaminationService(VcmsDbContext db) : ModuleService(db)
{
    private Task<OperationResult> Write(int actor, Func<User, Task<OperationResult>> action)
        => ExecuteAsync("VCMS:Care", actor, [AppRoles.Veterinarian], action);

    public Task<OperationResult> SaveEncounterAsync(int visitId, int actor, EncounterInput input, bool finalize = false, string? correctionReason = null)
        => Write(actor, async _ =>
        {
            var visit = await Db.Visits.Include(x => x.Appointment).Include(x => x.Encounter).SingleOrDefaultAsync(x => x.VisitID == visitId);
            if (visit is null || visit.Appointment.VeterinarianID != actor || visit.Status == VisitStatuses.Cancelled)
                return OperationResult.Fail("Chỉ bác sĩ được phân công mới được ghi bệnh án của lượt khám hợp lệ.");
            var row = visit.Encounter;
            if (row is not null && row.VeterinarianID != actor) return OperationResult.Fail("Bệnh án thuộc bác sĩ khác.");
            var correcting = row is not null && row.Status != "Draft";
            if (correcting && (string.IsNullOrWhiteSpace(correctionReason) || !Fits(correctionReason, 500)))
                return OperationResult.Fail("Cần lý do hiệu chỉnh (tối đa 500 ký tự). Bản cũ sẽ được lưu trong lịch sử.");
            if ((finalize || correcting) && string.IsNullOrWhiteSpace(input.Diagnosis))
                return OperationResult.Fail("Cần chẩn đoán trước khi chốt hoặc hiệu chỉnh bệnh án.");
            if (row is null && visit.Status == VisitStatuses.Completed)
                return OperationResult.Fail("Không mở bệnh án mới cho lượt khám đã hoàn tất.");
            object? before = row is null ? null : new { row.Symptoms, row.ExaminationNotes, row.Diagnosis, row.Status, row.FinalizedAt };
            if (row is null)
            {
                row = new Encounter { VisitID = visitId, VeterinarianID = actor, Status = "Draft", ExaminedAt = ClinicTime.UtcNow };
                Db.Encounters.Add(row);
            }
            row.Symptoms = Clean(input.Symptoms); row.ExaminationNotes = Clean(input.ExaminationNotes); row.Diagnosis = Clean(input.Diagnosis);
            if (correcting) row.Status = "Corrected";
            else if (finalize) { row.Status = "Finalized"; row.FinalizedAt = ClinicTime.UtcNow; }
            await Db.SaveChangesAsync();
            Audit(actor, "Encounter", row.EncounterID, correcting ? "Correct" : finalize ? "Finalize" : "SaveDraft", before,
                new { row.Symptoms, row.ExaminationNotes, row.Diagnosis, row.Status, row.FinalizedAt, CorrectionReason = Clean(correctionReason) });
            return OperationResult.Success(correcting ? "Đã lưu hiệu chỉnh và giữ lịch sử bệnh án." : "Đã lưu bệnh án.", row.EncounterID);
        });

    public Task<OperationResult> CreatePrescriptionAsync(int encounterId, int actor)
        => Write(actor, async _ =>
        {
            var encounter = await Db.Encounters.Include(x => x.Visit).ThenInclude(x => x.Appointment).SingleOrDefaultAsync(x => x.EncounterID == encounterId);
            if (encounter is null || encounter.VeterinarianID != actor || encounter.Visit.Appointment.VeterinarianID != actor || encounter.Visit.Status == VisitStatuses.Cancelled)
                return OperationResult.Fail("Không được kê đơn cho bệnh án này.");
            var existing = await Db.Prescriptions.SingleOrDefaultAsync(x => x.EncounterID == encounterId && x.Status != "Cancelled");
            if (existing is not null) return OperationResult.Success("Bệnh án đã có đơn thuốc đang sử dụng.", existing.PrescriptionID);
            var row = new Prescription { EncounterID = encounterId, PrescribedByUserID = actor, Status = "Draft", CreatedAt = ClinicTime.UtcNow };
            Db.Prescriptions.Add(row); await Db.SaveChangesAsync();
            Audit(actor, "Prescription", row.PrescriptionID, "Create", null, new { encounterId });
            return OperationResult.Success("Đã tạo đơn nháp.", row.PrescriptionID);
        });

    private Task<Prescription?> OwnedPrescription(int id, int actor) => Db.Prescriptions
        .Include(x => x.Encounter).ThenInclude(x => x.Visit).ThenInclude(x => x.Appointment)
        .Include(x => x.Items).ThenInclude(x => x.Medicine)
        .SingleOrDefaultAsync(x => x.PrescriptionID == id && x.PrescribedByUserID == actor && x.Encounter.VeterinarianID == actor
            && x.Encounter.Visit.Appointment.VeterinarianID == actor && x.Encounter.Visit.Status != VisitStatuses.Cancelled);

    public Task<OperationResult> SaveItemAsync(int prescriptionId, int? itemId, int actor, PrescriptionLineInput input, bool delete = false)
        => Write(actor, async _ =>
        {
            var rx = await OwnedPrescription(prescriptionId, actor);
            if (rx is null || rx.Status != "Draft") return OperationResult.Fail("Chỉ sửa được đơn nháp do bạn phụ trách.");
            var line = itemId is null ? null : rx.Items.SingleOrDefault(x => x.PrescriptionItemID == itemId);
            if (itemId is not null && line is null) return OperationResult.Fail("Không tìm thấy dòng thuốc trong đơn.");
            object? before = line is null ? null : new { line.MedicineID, line.Quantity, line.Dosage, line.Frequency, line.DurationDays, line.Instructions };
            if (delete)
            {
                if (line is null) return OperationResult.Fail("Không tìm thấy dòng thuốc.");
                Db.PrescriptionItems.Remove(line);
            }
            else
            {
                if (input.Quantity <= 0 || input.Quantity > 1000000 || string.IsNullOrWhiteSpace(input.Dosage)
                    || !Fits(input.Dosage, 200) || !Fits(input.Frequency, 200) || !Fits(input.Instructions, 500) || input.DurationDays is <= 0)
                    return OperationResult.Fail("Kiểm tra số lượng, liều dùng, số ngày và độ dài hướng dẫn.");
                if (!await Db.Medicines.AnyAsync(x => x.MedicineID == input.MedicineId && x.IsActive)) return OperationResult.Fail("Thuốc không còn hoạt động.");
                if (rx.Items.Any(x => x.MedicineID == input.MedicineId && x.PrescriptionItemID != itemId)) return OperationResult.Fail("Thuốc đã có trong đơn. Hãy sửa dòng hiện có.");
                if (line is null) { line = new PrescriptionItem { PrescriptionID = prescriptionId }; Db.PrescriptionItems.Add(line); }
                line.MedicineID = input.MedicineId; line.Quantity = input.Quantity; line.Dosage = input.Dosage.Trim();
                line.Frequency = Clean(input.Frequency); line.DurationDays = input.DurationDays; line.Instructions = Clean(input.Instructions);
            }
            Audit(actor, "Prescription", prescriptionId, delete ? "RemoveItem" : "SaveItem", before, new { itemId, input });
            return OperationResult.Success("Đã cập nhật dòng thuốc.", prescriptionId);
        });

    public Task<OperationResult> FinalizePrescriptionAsync(int id, int actor, string? instructions)
        => Write(actor, async _ =>
        {
            var rx = await OwnedPrescription(id, actor);
            if (rx is null || rx.Status != "Draft") return OperationResult.Fail("Chỉ chốt được đơn nháp do bạn phụ trách.");
            if (rx.Encounter.Status is not ("Finalized" or "Corrected")) return OperationResult.Fail("Chốt bệnh án trước khi chốt đơn thuốc.");
            if (!Fits(instructions, 1000) || rx.Items.Count == 0 || rx.Items.Any(x => !x.Medicine.IsActive || x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.Dosage)))
                return OperationResult.Fail("Đơn cần ít nhất một thuốc hợp lệ; hướng dẫn tối đa 1000 ký tự.");
            if (await Db.Prescriptions.AnyAsync(x => x.EncounterID == rx.EncounterID && x.PrescriptionID != id && x.Status == "Finalized"))
                return OperationResult.Fail("Bệnh án đã có đơn được chốt.");
            rx.Instructions = Clean(instructions); rx.Status = "Finalized"; rx.FinalizedAt = ClinicTime.UtcNow;
            // Existing schema has no snapshot columns: preserve the complete snapshot in append-only audit.
            Audit(actor, "Prescription", id, "Finalize", null, new PrescriptionSnapshot(id, rx.Instructions,
                rx.Items.Select(x => new MedicineSnapshot(x.PrescriptionItemID, x.MedicineID, x.Medicine.MedicineName, x.Medicine.Unit, x.Medicine.CurrentPrice, x.Quantity)).ToArray()));
            return OperationResult.Success("Đã chốt đơn. Tồn kho chưa bị trừ.", id);
        });

    public Task<OperationResult> CancelPrescriptionAsync(int id, int actor, string reason)
        => Write(actor, async _ =>
        {
            var rx = await OwnedPrescription(id, actor);
            if (rx is null || rx.Status == "Cancelled" || string.IsNullOrWhiteSpace(reason) || !Fits(reason, 500)) return OperationResult.Fail("Cần đơn hợp lệ và lý do hủy (tối đa 500 ký tự).");
            if (await Db.Dispensations.AnyAsync(x => x.PrescriptionID == id && x.Status == "Completed")) return OperationResult.Fail("Đã cấp thuốc, không được hủy đơn.");
            var before = new { rx.Status }; rx.Status = "Cancelled";
            Audit(actor, "Prescription", id, "Cancel", before, new { rx.Status, Reason = reason.Trim() });
            return OperationResult.Success("Đã hủy đơn thuốc.", id);
        });
}
