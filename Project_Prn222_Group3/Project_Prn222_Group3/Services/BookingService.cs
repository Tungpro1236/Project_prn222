using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;
namespace Project_Prn222_Group3.Services;

public record BookingRequest(int AnimalId, int ProviderId, int DurationMinutes, DateTime LocalStart, string? Reason, int CreatedByUserId);
public record BookingResult(bool Ok, string Message, int? AppointmentId = null);
public record AvailableSlot(DateTime LocalStart, DateTime LocalEnd);

// Compatibility for Tung's existing booking pages; schedules are read-only team data.
public class BookingService(VcmsDbContext db) : ModuleService(db)
{
    public async Task<IReadOnlyList<AvailableSlot>> GetAvailableSlotsAsync(int providerId, int durationMinutes, DateTime localDate, int? ignoreAppointmentId = null)
    {
        if (durationMinutes is < 5 or > 480 || localDate.Year is < 2000 or > 2099) return [];
        if (!await Db.Users.AnyAsync(x => x.UserID == providerId && x.IsActive && x.Role.RoleName == AppRoles.Veterinarian)) return [];
        var date = localDate.Date;
        var from = ClinicTime.VnLocalToUtc(date); var to = ClinicTime.VnLocalToUtc(date.AddDays(1));
        var schedules = await Db.VeterinarianSchedules.AsNoTracking().Where(x => x.VeterinarianID == providerId && x.StartAt < to && x.EndAt > from).ToListAsync();
        var appointments = await Db.Appointments.AsNoTracking().Where(x => x.VeterinarianID == providerId && x.AppointmentTime < to
            && x.AppointmentTime >= from.AddDays(-1) && AppointmentStatuses.Active.Contains(x.Status) && x.AppointmentID != ignoreAppointmentId).ToListAsync();
        var starts = new SortedSet<DateTime>();
        foreach (var schedule in schedules.Where(x => x.IsAvailable))
        {
            var start = schedule.StartAt > from ? schedule.StartAt : from;
            start = new DateTime(((start.Ticks + TimeSpan.FromMinutes(5).Ticks - 1) / TimeSpan.FromMinutes(5).Ticks) * TimeSpan.FromMinutes(5).Ticks, DateTimeKind.Unspecified);
            var limit = schedule.EndAt < to ? schedule.EndAt : to;
            for (; start.AddMinutes(durationMinutes) <= limit; start = start.AddMinutes(5))
            {
                var end = start.AddMinutes(durationMinutes);
                if (start <= ClinicTime.UtcNow || schedules.Any(x => !x.IsAvailable && x.StartAt < end && x.EndAt > start)
                    || appointments.Any(x => start < x.AppointmentTime.AddMinutes(x.ExpectedDurationMinutes) && end > x.AppointmentTime)) continue;
                starts.Add(start);
            }
        }
        return starts.Select(x => new AvailableSlot(ClinicTime.ToVietnam(x), ClinicTime.ToVietnam(x.AddMinutes(durationMinutes)))).ToArray();
    }
    public Task<BookingResult> BookAsync(BookingRequest request) => SaveAsync(null, request);
    public Task<BookingResult> RescheduleAsync(int id, BookingRequest request, int actorUserId, bool isClient)
        => actorUserId != request.CreatedByUserId ? Task.FromResult(new BookingResult(false, "Người thực hiện không hợp lệ.")) : SaveAsync(id, request);
    private async Task<BookingResult> SaveAsync(int? id, BookingRequest request)
    {
        var result = await ExecuteAsync("VCMS:Booking", request.CreatedByUserId, [AppRoles.Admin, AppRoles.ClinicManager, AppRoles.Receptionist, AppRoles.Client], async actor =>
        {
            var row = id is null ? new Appointment() : await Db.Appointments.Include(x => x.Animal).ThenInclude(x => x.Client).SingleOrDefaultAsync(x => x.AppointmentID == id);
            if (row is null || (id is not null && row.Status is not (AppointmentStatuses.Pending or AppointmentStatuses.Confirmed))) return OperationResult.Fail("Lịch hẹn không còn được phép thay đổi.");
            var client = actor.Role.RoleName == AppRoles.Client;
            if (id is not null && client && row.Animal.Client.UserID != actor.UserID) return OperationResult.Fail("Bạn chỉ được đổi lịch của chính mình.");
            var animal = await Db.Animals.Include(x => x.Client).SingleOrDefaultAsync(x => x.AnimalID == request.AnimalId && x.IsActive);
            if (animal is null || (client && animal.Client.UserID != actor.UserID)) return OperationResult.Fail("Thú cưng không hợp lệ hoặc không thuộc bạn.");
            if (!Fits(request.Reason, 500)) return OperationResult.Fail("Ghi chú tối đa 500 ký tự.");
            var slots = await GetAvailableSlotsAsync(request.ProviderId, request.DurationMinutes, request.LocalStart.Date, id);
            if (!slots.Any(x => x.LocalStart == request.LocalStart)) return OperationResult.Fail("Khung giờ đã kín hoặc ngoài lịch làm việc của bác sĩ.");
            row.AnimalID = request.AnimalId; row.VeterinarianID = request.ProviderId; row.ExpectedDurationMinutes = request.DurationMinutes;
            row.AppointmentTime = ClinicTime.VnLocalToUtc(request.LocalStart); row.Reason = Clean(request.Reason);
            row.Status = client ? AppointmentStatuses.Pending : AppointmentStatuses.Confirmed;
            if (id is null) { row.CreatedByUserID = actor.UserID; row.CreatedAt = ClinicTime.UtcNow; Db.Appointments.Add(row); }
            await Db.SaveChangesAsync();
            return OperationResult.Success("Đã lưu lịch hẹn.", row.AppointmentID);
        });
        return new(result.Ok, result.Message, result.Id);
    }
    public async Task<BookingResult> CancelAsync(int id, int actorUserId, bool isClient)
    {
        var result = await ExecuteAsync("VCMS:Booking", actorUserId, [AppRoles.Admin, AppRoles.ClinicManager, AppRoles.Receptionist, AppRoles.Client], async actor =>
        {
            var row = await Db.Appointments.AsNoTracking().Include(x => x.Animal).ThenInclude(x => x.Client).SingleOrDefaultAsync(x => x.AppointmentID == id);
            if (row is null || row.Status is not (AppointmentStatuses.Pending or AppointmentStatuses.Confirmed)) return OperationResult.Fail("Chỉ hủy được lịch chưa check-in.");
            if (actor.Role.RoleName == AppRoles.Client && row.Animal.Client.UserID != actor.UserID) return OperationResult.Fail("Bạn chỉ được hủy lịch của chính mình.");
            // Tung's conditional UPDATE cannot overwrite a concurrent check-in.
            // Keep the team-schema transaction, lock and current-actor checks.
            var cancellable = Db.Appointments.Where(x => x.AppointmentID == id
                && (x.Status == AppointmentStatuses.Pending || x.Status == AppointmentStatuses.Confirmed));
            if (actor.Role.RoleName == AppRoles.Client)
                cancellable = cancellable.Where(x => x.Animal.Client.UserID == actor.UserID);
            var cancelledAt = ClinicTime.UtcNow;
            var updated = await cancellable.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, AppointmentStatuses.Cancelled)
                .SetProperty(x => x.CancelledAt, cancelledAt));
            if (updated == 0) return OperationResult.Fail("Lịch hẹn đã thay đổi. Vui lòng tải lại trang.");
            return OperationResult.Success("Đã hủy lịch hẹn.", id);
        });
        return new(result.Ok, result.Message, result.Id);
    }
}
