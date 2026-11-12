using System.Data;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Data;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Services;

public record BookingRequest(
    int AnimalId,
    int ProviderId,
    int ServiceId,
    DateTime LocalStart,
    string? Reason,
    int CreatedByUserId);

public record BookingResult(bool Ok, string Message, int? AppointmentId = null);

public record AvailableSlot(DateTime LocalStart, DateTime LocalEnd);

public class BookingService
{
    private readonly VcmsDbContext _db;

    public BookingService(VcmsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AvailableSlot>> GetAvailableSlotsAsync(
        int providerId,
        int serviceId,
        DateTime localDate,
        int? ignoreAppointmentId = null)
    {
        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.ServiceID == serviceId && s.IsActive);
        var setting = await _db.ClinicSettings.AsNoTracking().OrderBy(s => s.ClinicSettingID).FirstOrDefaultAsync();
        if (service is null || setting is null)
        {
            return [];
        }

        var date = localDate.Date;
        var dayOfWeek = (byte)date.DayOfWeek;
        var schedule = await _db.StaffSchedules.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserID == providerId && s.DayOfWeek == dayOfWeek && s.IsWorking);
        if (schedule is null)
        {
            return [];
        }

        var clinicOpen = TimeSpan.FromHours(setting.OpenHour);
        var clinicClose = TimeSpan.FromHours(setting.CloseHour);
        var windowStart = schedule.StartTime > clinicOpen ? schedule.StartTime : clinicOpen;
        var windowEnd = schedule.EndTime < clinicClose ? schedule.EndTime : clinicClose;
        if (windowEnd <= windowStart)
        {
            return [];
        }

        var dayStartUtc = ClinicTime.VnLocalToUtc(date);
        var dayEndUtc = ClinicTime.VnLocalToUtc(date.AddDays(1));
        var appointments = await _db.Appointments.AsNoTracking()
            .Where(a => a.VeterinarianID == providerId
                && a.AppointmentTime >= dayStartUtc
                && a.AppointmentTime < dayEndUtc
                && AppointmentStatuses.Active.Contains(a.Status)
                && a.AppointmentID != ignoreAppointmentId)
            .Select(a => new { a.AppointmentTime, a.ExpectedDurationMinutes })
            .ToListAsync();

        var blocked = await _db.BlockedTimes.AsNoTracking()
            .Where(b => b.UserID == providerId && b.StartAt < dayEndUtc && b.EndAt > dayStartUtc)
            .Select(b => new { b.StartAt, b.EndAt })
            .ToListAsync();

        var duration = TimeSpan.FromMinutes(service.DurationMinutes);
        var step = TimeSpan.FromMinutes(Math.Max(5, setting.SlotMinutes));
        var now = ClinicTime.ToVietnam(ClinicTime.UtcNow);
        var slots = new List<AvailableSlot>();

        for (var start = date.Add(windowStart); start.Add(duration) <= date.Add(windowEnd); start = start.Add(step))
        {
            var end = start.Add(duration);
            if (start <= now)
            {
                continue;
            }

            var startUtc = ClinicTime.VnLocalToUtc(start);
            var endUtc = ClinicTime.VnLocalToUtc(end);
            var overlapsAppointment = appointments.Any(a =>
                startUtc < a.AppointmentTime.AddMinutes(a.ExpectedDurationMinutes) &&
                endUtc > a.AppointmentTime);
            var overlapsBlock = blocked.Any(b => startUtc < b.EndAt && endUtc > b.StartAt);

            if (!overlapsAppointment && !overlapsBlock)
            {
                slots.Add(new AvailableSlot(start, end));
            }
        }

        return slots;
    }

    public async Task<BookingResult> BookAsync(BookingRequest request)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var result = await SaveAppointmentAsync(request, appointmentId: null);
                if (!result.Ok)
                {
                    await transaction.RollbackAsync();
                    return result;
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch (DbUpdateException) when (attempt == 1)
            {
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();
                return new BookingResult(false, "Không thể lưu lịch hẹn lúc này. Vui lòng thử lại.");
            }
        }

        return new BookingResult(false, "Không thể đặt lịch. Vui lòng thử lại.");
    }

    public async Task<BookingResult> RescheduleAsync(int appointmentId, BookingRequest request, int actorUserId, bool isClient)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var appointment = await _db.Appointments
                    .Include(a => a.Animal)
                        .ThenInclude(a => a.Client)
                    .FirstOrDefaultAsync(a => a.AppointmentID == appointmentId);
                if (appointment is null)
                {
                    return new BookingResult(false, "Không tìm thấy lịch hẹn.");
                }

                if (isClient && appointment.Animal.Client.UserID != actorUserId)
                {
                    return new BookingResult(false, "Bạn chỉ có thể đổi lịch hẹn của chính mình.");
                }

                if (appointment.Status is not (AppointmentStatuses.Pending or AppointmentStatuses.Confirmed))
                {
                    return new BookingResult(false, "Chỉ có thể đổi lịch hẹn đang chờ xác nhận hoặc đã xác nhận.");
                }

                var validation = await ValidateAsync(request, appointmentId);
                if (!validation.Ok)
                {
                    await transaction.RollbackAsync();
                    return new BookingResult(false, validation.Message);
                }

                appointment.AnimalID = request.AnimalId;
                appointment.ClientID = validation.ClientId;
                appointment.ServiceID = request.ServiceId;
                appointment.VeterinarianID = request.ProviderId;
                appointment.AppointmentTime = ClinicTime.VnLocalToUtc(request.LocalStart);
                appointment.ExpectedDurationMinutes = validation.DurationMinutes;
                appointment.Reason = Clean(request.Reason);
                appointment.Status = isClient ? AppointmentStatuses.Pending : AppointmentStatuses.Confirmed;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return new BookingResult(true, "Đã cập nhật lịch hẹn.", appointment.AppointmentID);
            }
            catch (DbUpdateException) when (attempt == 1)
            {
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();
                return new BookingResult(false, "Không thể cập nhật lịch hẹn lúc này. Vui lòng thử lại.");
            }
        }

        return new BookingResult(false, "Không thể đổi lịch. Vui lòng thử lại.");
    }

    public async Task<BookingResult> CancelAsync(int appointmentId, int actorUserId, bool isClient)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Animal)
                .ThenInclude(a => a.Client)
            .FirstOrDefaultAsync(a => a.AppointmentID == appointmentId);
        if (appointment is null)
        {
            return new BookingResult(false, "Không tìm thấy lịch hẹn.");
        }

        if (isClient && appointment.Animal.Client.UserID != actorUserId)
        {
            return new BookingResult(false, "Bạn chỉ có thể hủy lịch hẹn của chính mình.");
        }

        if (appointment.Status is not (AppointmentStatuses.Pending or AppointmentStatuses.Confirmed))
        {
            return new BookingResult(false, "Chỉ có thể hủy lịch hẹn chưa check-in.");
        }

        appointment.Status = AppointmentStatuses.Cancelled;
        appointment.CancelledAt = ClinicTime.UtcNow;
        await _db.SaveChangesAsync();
        return new BookingResult(true, "Đã hủy lịch hẹn.", appointment.AppointmentID);
    }

    private async Task<BookingResult> SaveAppointmentAsync(BookingRequest request, int? appointmentId)
    {
        var validation = await ValidateAsync(request, appointmentId);
        if (!validation.Ok)
        {
            return new BookingResult(false, validation.Message);
        }

        var appointment = new Appointment
        {
            AnimalID = request.AnimalId,
            ClientID = validation.ClientId,
            ServiceID = request.ServiceId,
            VeterinarianID = request.ProviderId,
            AppointmentTime = ClinicTime.VnLocalToUtc(request.LocalStart),
            ExpectedDurationMinutes = validation.DurationMinutes,
            Reason = Clean(request.Reason),
            Status = AppointmentStatuses.Confirmed,
            CreatedByUserID = request.CreatedByUserId,
            CreatedAt = ClinicTime.UtcNow
        };

        var creator = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == request.CreatedByUserId);
        if (creator?.Role.RoleName == AppRoles.Client)
        {
            appointment.Status = AppointmentStatuses.Pending;
        }

        _db.Appointments.Add(appointment);
        return new BookingResult(true, "Đã đặt lịch hẹn.", appointment.AppointmentID);
    }

    private async Task<ValidationResult> ValidateAsync(BookingRequest request, int? ignoreAppointmentId)
    {
        if (request.LocalStart.Minute % 5 != 0 || request.LocalStart.Second != 0)
        {
            return ValidationResult.Fail("Thời gian hẹn không hợp lệ.");
        }

        var animal = await _db.Animals.AsNoTracking()
            .Include(a => a.Client)
            .FirstOrDefaultAsync(a => a.AnimalID == request.AnimalId && a.IsActive);
        if (animal is null)
        {
            return ValidationResult.Fail("Không tìm thấy thú cưng đang hoạt động.");
        }

        var actor = await _db.Users.AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserID == request.CreatedByUserId && u.IsActive);
        if (actor is null)
        {
            return ValidationResult.Fail("Không xác định được người đặt lịch.");
        }

        if (actor.Role.RoleName == AppRoles.Client && animal.Client.UserID != actor.UserID)
        {
            return ValidationResult.Fail("Bạn chỉ có thể đặt lịch cho thú cưng của chính mình.");
        }

        var service = await _db.Services.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceID == request.ServiceId && s.IsActive);
        if (service is null)
        {
            return ValidationResult.Fail("Dịch vụ không còn hoạt động.");
        }

        var provider = await _db.Users.AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserID == request.ProviderId && u.IsActive);
        if (provider is null || !MatchesCategory(provider.Role.RoleName, service.Category))
        {
            return ValidationResult.Fail("Nhân viên không phù hợp với dịch vụ đã chọn.");
        }

        var slots = await GetAvailableSlotsAsync(request.ProviderId, request.ServiceId, request.LocalStart.Date, ignoreAppointmentId);
        if (!slots.Any(s => s.LocalStart == request.LocalStart))
        {
            return ValidationResult.Fail("Khung giờ này đã kín, nằm ngoài ca làm hoặc trùng thời gian nghỉ.");
        }

        return new ValidationResult(true, string.Empty, animal.ClientID, service.DurationMinutes);
    }

    private static bool MatchesCategory(string roleName, string category) =>
        category == ServiceCategories.Grooming
            ? roleName == AppRoles.PetGroomer
            : roleName == AppRoles.Veterinarian;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ValidationResult(bool Ok, string Message, int ClientId = 0, int DurationMinutes = 0)
    {
        public static ValidationResult Fail(string message) => new(false, message);
    }
}
