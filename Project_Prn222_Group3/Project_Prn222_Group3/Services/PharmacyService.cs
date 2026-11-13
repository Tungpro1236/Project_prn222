using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Services;

public record MedicineInput(string MedicineName, string Unit, decimal CurrentPrice, bool IsActive);
public record DispenseLine(int PrescriptionItemId, int BatchId, int Quantity);

public class PharmacyService(VcmsDbContext db) : ModuleService(db)
{
    private Task<OperationResult> Write(int actor, Func<User, Task<OperationResult>> action)
        => ExecuteAsync("VCMS:Care", actor, [AppRoles.Admin, AppRoles.ClinicManager, AppRoles.DispensingStaff], action);

    public Task<OperationResult> SaveMedicineAsync(int? id, int actor, MedicineInput input)
        => Write(actor, async _ =>
        {
            if (string.IsNullOrWhiteSpace(input.MedicineName) || string.IsNullOrWhiteSpace(input.Unit)
                || !Fits(input.MedicineName, 150) || !Fits(input.Unit, 30) || input.CurrentPrice < 0 || input.CurrentPrice > 1000000000m
                || decimal.Round(input.CurrentPrice, 2) != input.CurrentPrice)
                return OperationResult.Fail("Nhập tên, đơn vị và giá hợp lệ (tối đa 2 số thập phân).");
            var row = id is null ? new Medicine() : await Db.Medicines.SingleOrDefaultAsync(x => x.MedicineID == id);
            if (row is null) return OperationResult.Fail("Không tìm thấy thuốc.");
            object? before = id is null ? null : new { row.MedicineName, row.Unit, row.CurrentPrice, row.IsActive };
            row.MedicineName = input.MedicineName.Trim(); row.Unit = input.Unit.Trim(); row.CurrentPrice = input.CurrentPrice; row.IsActive = input.IsActive;
            if (id is null) Db.Medicines.Add(row);
            await Db.SaveChangesAsync();
            Audit(actor, "Medicine", row.MedicineID, id is null ? "Create" : "Update", before, input);
            return OperationResult.Success("Đã lưu thuốc.", row.MedicineID);
        });

    public Task<OperationResult> ReceiveAsync(int medicineId, int actor, string batchNumber, DateTime expiry, int quantity, decimal? cost, string? reason)
        => Write(actor, async _ =>
        {
            if (string.IsNullOrWhiteSpace(batchNumber) || !Fits(batchNumber, 80) || batchNumber.Any(c => c > 127)
                || expiry.Date < ClinicTime.VnToday || quantity is <= 0 or > 1000000 || cost is < 0 or > 1000000000m
                || (cost is not null && decimal.Round(cost.Value, 2) != cost) || !Fits(reason, 500))
                return OperationResult.Fail("Kiểm tra mã lô (ký tự ASCII), hạn dùng, số lượng và giá nhập.");
            if (!await Db.Medicines.AnyAsync(x => x.MedicineID == medicineId && x.IsActive)) return OperationResult.Fail("Thuốc không còn hoạt động.");
            var code = batchNumber.Trim();
            var batch = await Db.MedicineBatches.SingleOrDefaultAsync(x => x.MedicineID == medicineId && x.BatchNumber == code);
            if (batch is null)
            {
                batch = new MedicineBatch { MedicineID = medicineId, BatchNumber = code, ExpiryDate = expiry.Date, PurchaseUnitCost = cost, ReceivedAt = ClinicTime.UtcNow };
                Db.MedicineBatches.Add(batch); await Db.SaveChangesAsync();
            }
            else if (batch.ExpiryDate.Date != expiry.Date || batch.PurchaseUnitCost != cost)
                return OperationResult.Fail("Lô đã tồn tại với hạn dùng hoặc giá nhập khác. Kiểm tra lại thông tin lô.");
            if ((long)batch.QuantityOnHand + quantity > int.MaxValue) return OperationResult.Fail("Số lượng tồn vượt giới hạn.");
            var previous = batch.QuantityOnHand; batch.QuantityOnHand += quantity;
            Db.StockMovements.Add(new StockMovement { BatchID = batch.BatchID, MovementType = "Receipt", QuantityChange = quantity, PerformedByUserID = actor, Reason = Clean(reason), CreatedAt = ClinicTime.UtcNow });
            Audit(actor, "MedicineBatch", batch.BatchID, "Receive", new { QuantityOnHand = previous }, new { batch.QuantityOnHand, quantity, Reason = Clean(reason) });
            return OperationResult.Success("Đã nhập lô thuốc và ghi sổ kho.", batch.BatchID);
        });

    public Task<OperationResult> AdjustAsync(int batchId, int actor, int delta, string reason)
        => Write(actor, async _ =>
        {
            if (delta == 0 || delta is < -1000000 or > 1000000 || string.IsNullOrWhiteSpace(reason) || !Fits(reason, 500))
                return OperationResult.Fail("Nhập mức điều chỉnh khác 0 và lý do (tối đa 500 ký tự).");
            var batch = await Db.MedicineBatches.SingleOrDefaultAsync(x => x.BatchID == batchId);
            if (batch is null || (long)batch.QuantityOnHand + delta < 0 || (long)batch.QuantityOnHand + delta > int.MaxValue)
                return OperationResult.Fail("Không tìm thấy lô hoặc điều chỉnh làm tồn kho không hợp lệ.");
            var previous = batch.QuantityOnHand; batch.QuantityOnHand += delta;
            Db.StockMovements.Add(new StockMovement { BatchID = batchId, MovementType = "Adjustment", QuantityChange = delta, Reason = reason.Trim(), PerformedByUserID = actor, CreatedAt = ClinicTime.UtcNow });
            Audit(actor, "MedicineBatch", batchId, "Adjust", new { QuantityOnHand = previous }, new { batch.QuantityOnHand, delta, Reason = reason.Trim() });
            return OperationResult.Success("Đã điều chỉnh tồn kho và ghi lý do.", batchId);
        });

    public Task<OperationResult> DispenseAsync(int prescriptionId, int actor, Guid requestKey, IReadOnlyList<DispenseLine> lines)
        => Write(actor, async _ =>
        {
            if (requestKey == Guid.Empty || lines.Count is 0 or > 200 || lines.Any(x => x.Quantity is <= 0 or > 1000000)
                || lines.GroupBy(x => new { x.PrescriptionItemId, x.BatchId }).Any(g => g.Count() != 1))
                return OperationResult.Fail("Chọn ít nhất một lô và số lượng dương; mỗi dòng thuốc/lô chỉ xuất hiện một lần.");
            var existing = await Db.Dispensations.Include(x => x.Items).SingleOrDefaultAsync(x => x.RequestKey == requestKey);
            if (existing is not null)
            {
                var same = existing.PrescriptionID == prescriptionId && existing.DispensedByUserID == actor && existing.Status == "Completed"
                    && existing.Items.Count == lines.Count && existing.Items.All(x => lines.Any(l => l.PrescriptionItemId == x.PrescriptionItemID && l.BatchId == x.BatchID && l.Quantity == x.Quantity));
                return same ? OperationResult.Success("Yêu cầu này đã được cấp thuốc trước đó; không trừ kho lần nữa.", existing.DispensationID)
                    : OperationResult.Fail("Mã yêu cầu đã được dùng cho nội dung khác. Tải lại trang.");
            }
            var rx = await Db.Prescriptions.Include(x => x.Encounter).ThenInclude(x => x.Visit)
                .Include(x => x.Items).ThenInclude(x => x.Medicine).SingleOrDefaultAsync(x => x.PrescriptionID == prescriptionId);
            if (rx is null || rx.Status != "Finalized" || rx.Encounter.Status is not ("Finalized" or "Corrected") || rx.Encounter.Visit.Status == VisitStatuses.Cancelled)
                return OperationResult.Fail("Chỉ cấp thuốc cho đơn và bệnh án đã chốt, lượt khám chưa hủy.");
            var snapshot = await PrescriptionSnapshots.ReadAsync(Db, prescriptionId);
            if (snapshot is null || snapshot.Lines.Length != rx.Items.Count || snapshot.Lines.Any(s => !rx.Items.Any(i => i.PrescriptionItemID == s.ItemId && i.MedicineID == s.MedicineId && i.Quantity == s.Quantity)))
                return OperationResult.Fail("Đơn thiếu bản chụp hợp lệ khi chốt. Liên hệ bác sĩ để kiểm tra; không tự lấy giá hiện tại.");

            var batchIds = lines.Select(x => x.BatchId).Distinct().ToArray();
            var batches = await Db.MedicineBatches.Where(x => batchIds.Contains(x.BatchID)).ToDictionaryAsync(x => x.BatchID);
            foreach (var group in lines.GroupBy(x => x.PrescriptionItemId))
            {
                var item = rx.Items.SingleOrDefault(x => x.PrescriptionItemID == group.Key);
                if (item is null || !item.Medicine.IsActive) return OperationResult.Fail("Dòng thuốc không thuộc đơn hoặc thuốc đã ngừng dùng.");
                var issued = await Db.DispensationItems.Where(x => x.PrescriptionItemID == group.Key && x.Dispensation.Status == "Completed").SumAsync(x => (long)x.Quantity);
                if (issued + group.Sum(x => (long)x.Quantity) > item.Quantity) return OperationResult.Fail("Tổng số lượng cấp vượt số lượng kê đơn còn lại.");
                foreach (var line in group)
                    if (!batches.TryGetValue(line.BatchId, out var batch) || batch.MedicineID != item.MedicineID || batch.ExpiryDate.Date < ClinicTime.VnToday)
                        return OperationResult.Fail("Lô không đúng thuốc hoặc đã hết hạn.");
            }
            foreach (var group in lines.GroupBy(x => x.BatchId))
                if (group.Sum(x => (long)x.Quantity) > batches[group.Key].QuantityOnHand) return OperationResult.Fail("Lô thuốc không đủ tồn kho.");

            var dispense = new Dispensation { PrescriptionID = prescriptionId, DispensedByUserID = actor, RequestKey = requestKey, Status = "Draft" };
            foreach (var line in lines)
            {
                var batch = batches[line.BatchId]; batch.QuantityOnHand -= line.Quantity;
                var item = new DispensationItem { PrescriptionItemID = line.PrescriptionItemId, BatchID = line.BatchId, Quantity = line.Quantity,
                    UnitPrice = snapshot.Lines.Single(x => x.ItemId == line.PrescriptionItemId).UnitPrice };
                dispense.Items.Add(item);
                Db.StockMovements.Add(new StockMovement { BatchID = line.BatchId, DispensationItem = item, MovementType = "Dispense", QuantityChange = -line.Quantity, PerformedByUserID = actor, CreatedAt = ClinicTime.UtcNow });
            }
            Db.Dispensations.Add(dispense);
            // Child records and movements must exist before Completed is persisted, in this transaction.
            await Db.SaveChangesAsync();
            dispense.Status = "Completed"; dispense.DispensedAt = ClinicTime.UtcNow;
            Audit(actor, "Dispensation", dispense.DispensationID, "Complete", null, new { prescriptionId, requestKey, lines });
            return OperationResult.Success("Đã cấp thuốc, trừ tồn kho và ghi sổ xuất.", dispense.DispensationID);
        });
}
