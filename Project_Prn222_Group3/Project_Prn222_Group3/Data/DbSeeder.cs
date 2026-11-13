using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VcmsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        if (!await db.Database.CanConnectAsync())
        {
            throw new InvalidOperationException(
                "Không kết nối được VeterinaryClinicDB. Hãy chạy file VCMS_Database.sql bằng SSMS trước khi chạy ứng dụng.");
        }

        var roles = await EnsureRolesAsync(db);
        var manager = await EnsureUserAsync(db, hasher, roles, "manager", "Manager@123", "Quản lý phòng khám", AppRoles.ClinicManager, "manager@vcms.local", "0900000001");
        var reception = await EnsureUserAsync(db, hasher, roles, "reception", "Reception@123", "Lễ tân Ngọc Lan", AppRoles.Receptionist, "reception@vcms.local", "0900000002");
        var vet = await EnsureUserAsync(db, hasher, roles, "vet1", "Vet@123", "Bác sĩ Minh", AppRoles.Veterinarian, "vet1@vcms.local", "0900000003");
        var clientUser = await EnsureUserAsync(db, hasher, roles, "client1", "Client@123", "Nguyễn Văn An", AppRoles.Client, "client1@vcms.local", "0901234567");
        var groomer = await EnsureUserAsync(db, hasher, roles, "groomer1", "Groomer@123", "Nhân viên Hà", AppRoles.PetGroomer, "groomer1@vcms.local", "0900000004");

        var client = await EnsureDemoClientAsync(db, clientUser);
        var miu = await EnsureAnimalAsync(db, client, "Miu", "Mèo", "Mèo ta", "Female", 3.2m);
        var bong = await EnsureAnimalAsync(db, client, "Bông", "Chó", "Poodle", "Male", 12.5m);

        await EnsureClinicSettingAsync(db);
        var exam = await EnsureServiceAsync(db, "Khám tổng quát", ServiceCategories.Veterinary, 30, 150000, "Khám lâm sàng và tư vấn.");
        var vaccine = await EnsureServiceAsync(db, "Tiêm phòng", ServiceCategories.Veterinary, 20, 250000, "Tiêm vaccine theo lịch.");
        var grooming = await EnsureServiceAsync(db, "Tắm và cắt tỉa", ServiceCategories.Grooming, 60, 300000, "Tắm, sấy và cắt tỉa lông.");

        await EnsureWeekdayScheduleAsync(db, vet, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        await EnsureWeekdayScheduleAsync(db, groomer, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0));
        await EnsureDemoBlockAsync(db, vet);

        await EnsureDemoAppointmentAsync(db, miu, vet, reception, exam, 9, isConfirmed: true);
        await EnsureDemoAppointmentAsync(db, bong, vet, reception, vaccine, 10, isConfirmed: false);

        logger.LogInformation("VCMS demo data is ready. Demo accounts: manager, reception, vet1, groomer1, client1.");
    }

    private static async Task<Dictionary<string, Role>> EnsureRolesAsync(VcmsDbContext db)
    {
        var existing = await db.Roles.ToListAsync();
        var map = existing.ToDictionary(r => r.RoleName, StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in AppRoles.StaffRoleNames.Append(AppRoles.Client))
        {
            if (map.ContainsKey(roleName))
            {
                continue;
            }

            var role = new Role { RoleName = roleName, Description = AppRoles.DisplayName(roleName) };
            db.Roles.Add(role);
            map[roleName] = role;
        }

        await db.SaveChangesAsync();
        return map;
    }

    private static async Task<User> EnsureUserAsync(
        VcmsDbContext db,
        IPasswordHasher<User> hasher,
        Dictionary<string, Role> roles,
        string username,
        string password,
        string fullName,
        string roleName,
        string email,
        string phone)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (existing is not null)
        {
            return existing;
        }

        var user = new User
        {
            Username = username,
            FullName = fullName,
            Email = email,
            Phone = phone,
            RoleID = roles[roleName].RoleID,
            IsActive = true,
            CreatedAt = ClinicTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Client> EnsureDemoClientAsync(VcmsDbContext db, User clientUser)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Phone == "0901234567");
        if (client is null)
        {
            client = new Client
            {
                UserID = clientUser.UserID,
                FullName = "Nguyễn Văn An",
                Phone = "0901234567",
                Email = "client1@vcms.local",
                Address = "Quận 1, TP. Hồ Chí Minh",
                CreatedAt = ClinicTime.UtcNow
            };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            return client;
        }

        if (client.UserID is null)
        {
            client.UserID = clientUser.UserID;
            await db.SaveChangesAsync();
        }

        return client;
    }

    private static async Task<Animal> EnsureAnimalAsync(
        VcmsDbContext db,
        Client client,
        string name,
        string species,
        string breed,
        string gender,
        decimal weightKg)
    {
        var animal = await db.Animals.FirstOrDefaultAsync(a => a.ClientID == client.ClientID && a.Name == name);
        if (animal is not null)
        {
            return animal;
        }

        animal = new Animal
        {
            ClientID = client.ClientID,
            Name = name,
            Species = species,
            Breed = breed,
            Gender = gender,
            WeightKg = weightKg,
            IsActive = true
        };
        db.Animals.Add(animal);
        await db.SaveChangesAsync();
        return animal;
    }

    private static async Task EnsureDemoAppointmentAsync(
        VcmsDbContext db,
        Animal animal,
        User provider,
        User createdBy,
        ClinicService service,
        int hour,
        bool isConfirmed)
    {
        var today = ClinicTime.VnToday;
        var dayStart = ClinicTime.VnLocalToUtc(today);
        var dayEnd = ClinicTime.VnLocalToUtc(today.AddDays(1));

        var animalHasAppointmentToday = await db.Appointments
            .AnyAsync(a => a.AnimalID == animal.AnimalID && a.AppointmentTime >= dayStart && a.AppointmentTime < dayEnd);
        if (animalHasAppointmentToday)
        {
            return;
        }

        var appointmentTime = ClinicTime.VnLocalToUtc(today.AddHours(hour));
        var status = isConfirmed ? AppointmentStatuses.Confirmed : AppointmentStatuses.Pending;

        if (isConfirmed)
        {
            var slotTaken = await db.Appointments.AnyAsync(a =>
                a.VeterinarianID == provider.UserID &&
                a.AppointmentTime == appointmentTime &&
                AppointmentStatuses.Active.Contains(a.Status));
            if (slotTaken)
            {
                return;
            }
        }

        db.Appointments.Add(new Appointment
        {
            AnimalID = animal.AnimalID,
            ClientID = animal.ClientID,
            ServiceID = service.ServiceID,
            VeterinarianID = provider.UserID,
            AppointmentTime = appointmentTime,
            ExpectedDurationMinutes = service.DurationMinutes,
            Reason = service.Name,
            Status = status,
            CreatedByUserID = createdBy.UserID,
            CreatedAt = ClinicTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureClinicSettingAsync(VcmsDbContext db)
    {
        if (await db.ClinicSettings.AnyAsync())
        {
            return;
        }

        db.ClinicSettings.Add(new ClinicSetting
        {
            ClinicName = "Phòng khám thú y VCMS",
            Address = "Quận 1, TP. Hồ Chí Minh",
            Phone = "0280000000",
            Email = "clinic@vcms.local",
            SlotMinutes = 30,
            OpenHour = 8,
            CloseHour = 17,
            UpdatedAt = ClinicTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<ClinicService> EnsureServiceAsync(
        VcmsDbContext db,
        string name,
        string category,
        int durationMinutes,
        decimal price,
        string description)
    {
        var service = await db.Services.FirstOrDefaultAsync(s => s.Name == name);
        if (service is not null)
        {
            return service;
        }

        service = new ClinicService
        {
            Name = name,
            Category = category,
            DurationMinutes = durationMinutes,
            Price = price,
            Description = description,
            IsActive = true
        };
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return service;
    }

    private static async Task EnsureWeekdayScheduleAsync(VcmsDbContext db, User user, TimeSpan start, TimeSpan end)
    {
        var existingDays = await db.StaffSchedules
            .Where(s => s.UserID == user.UserID)
            .Select(s => s.DayOfWeek)
            .ToListAsync();

        for (byte day = 1; day <= 6; day++)
        {
            if (existingDays.Contains(day))
            {
                continue;
            }

            db.StaffSchedules.Add(new StaffSchedule
            {
                UserID = user.UserID,
                DayOfWeek = day,
                StartTime = start,
                EndTime = end,
                IsWorking = true
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureDemoBlockAsync(VcmsDbContext db, User user)
    {
        var tomorrow = ClinicTime.VnToday.AddDays(1);
        var start = ClinicTime.VnLocalToUtc(tomorrow.AddHours(12));
        var end = ClinicTime.VnLocalToUtc(tomorrow.AddHours(13));
        var exists = await db.BlockedTimes.AnyAsync(b => b.UserID == user.UserID && b.StartAt == start);
        if (exists)
        {
            return;
        }

        db.BlockedTimes.Add(new BlockedTime
        {
            UserID = user.UserID,
            StartAt = start,
            EndAt = end,
            Reason = "Nghỉ trưa"
        });
        await db.SaveChangesAsync();
    }
}
