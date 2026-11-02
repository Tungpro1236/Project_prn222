using System.Data;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Services;

public enum QueueAction
{
    Call,
    Serve,
    Done,
    Skip
}

public record CheckInResult(bool Ok, string Message, int? QueueNumber = null);

public class ReceptionService
{
    private readonly VcmsDbContext _db;

    public ReceptionService(VcmsDbContext db)
    {
        _db = db;
    }

    public async Task<CheckInResult> CheckInAsync(int appointmentId, int receptionistId)
    {
        var today = ClinicTime.VnToday;
        var dayStart = ClinicTime.VnLocalToUtc(today);
        var dayEnd = ClinicTime.VnLocalToUtc(today.AddDays(1));

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var appointment = await _db.Appointments
                    .Include(a => a.Visit)
                    .FirstOrDefaultAsync(a => a.AppointmentID == appointmentId);

                if (appointment is null)
                {
                    return new CheckInResult(false, "Không tìm thấy lịch hẹn.");
                }

                if (appointment.Visit is not null)
                {
                    return new CheckInResult(false, "Lịch hẹn này đã được check-in trước đó.");
                }

                if (appointment.Status is not (AppointmentStatuses.Pending or AppointmentStatuses.Confirmed))
                {
                    return new CheckInResult(false, "Lịch hẹn không ở trạng thái có thể check-in.");
                }

                if (appointment.AppointmentTime < dayStart || appointment.AppointmentTime >= dayEnd)
                {
                    return new CheckInResult(false, "Chỉ có thể check-in lịch hẹn trong ngày hôm nay.");
                }

                var lastNumber = await _db.QueueEntries
                    .Where(q => q.QueueDate == today)
                    .MaxAsync(q => (int?)q.QueueNumber) ?? 0;
                var queueNumber = lastNumber + 1;

                var visit = new Visit
                {
                    AppointmentID = appointment.AppointmentID,
                    CheckInAt = ClinicTime.UtcNow,
                    ReceptionistID = receptionistId,
                    Status = VisitStatuses.Waiting
                };
                _db.Visits.Add(visit);
                _db.QueueEntries.Add(new QueueEntry
                {
                    Visit = visit,
                    QueueDate = today,
                    QueueNumber = queueNumber,
                    Status = QueueStatuses.Waiting
                });

                appointment.Status = AppointmentStatuses.CheckedIn;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return new CheckInResult(true, $"Check-in thành công. Số thứ tự: {queueNumber}", queueNumber);
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();
                await Task.Delay(50 * attempt);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();
                return new CheckInResult(false, "Không thể cấp số thứ tự lúc này. Vui lòng thử lại.");
            }
        }

        return new CheckInResult(false, "Không thể check-in. Vui lòng thử lại.");
    }

    public async Task<(bool Ok, string Message)> AdvanceQueueAsync(int queueEntryId, QueueAction action)
    {
        var today = ClinicTime.VnToday;
        var entry = await _db.QueueEntries
            .Include(q => q.Visit)
            .FirstOrDefaultAsync(q => q.QueueEntryID == queueEntryId && q.QueueDate == today);

        if (entry is null)
        {
            return (false, "Không tìm thấy lượt trong hàng đợi hôm nay.");
        }

        var now = ClinicTime.UtcNow;

        switch (action)
        {
            case QueueAction.Call:
                if (entry.Status != QueueStatuses.Waiting)
                {
                    return (false, "Chỉ có thể gọi lượt đang chờ.");
                }

                entry.Status = QueueStatuses.Called;
                entry.CalledAt = now;
                break;

            case QueueAction.Serve:
                if (entry.Status != QueueStatuses.Called)
                {
                    return (false, "Chỉ có thể bắt đầu phục vụ lượt đã được gọi.");
                }

                entry.Status = QueueStatuses.Serving;
                entry.CalledAt ??= now;
                entry.Visit.Status = VisitStatuses.InProgress;
                break;

            case QueueAction.Done:
                if (entry.Status != QueueStatuses.Serving)
                {
                    return (false, "Chỉ có thể hoàn tất lượt đang phục vụ.");
                }

                entry.Status = QueueStatuses.Done;
                entry.Visit.Status = VisitStatuses.Completed;
                entry.Visit.CompletedAt = now;
                break;

            case QueueAction.Skip:
                if (entry.Status is not (QueueStatuses.Waiting or QueueStatuses.Called))
                {
                    return (false, "Chỉ có thể bỏ qua lượt đang chờ hoặc đã gọi.");
                }

                entry.Status = QueueStatuses.Skipped;
                entry.Visit.Status = VisitStatuses.Cancelled;
                break;
        }

        await _db.SaveChangesAsync();
        return (true, $"Đã cập nhật số {entry.QueueNumber} sang trạng thái \"{DisplayText.QueueStatus(entry.Status)}\".");
    }
}
