using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Prn222_Group3.Models;

[Table("Roles")]
public class Role
{
    [Key]
    public int RoleID { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }
}

[Table("Users")]
public class User
{
    [Key]
    public int UserID { get; set; }

    public int RoleID { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;

    public Client? Client { get; set; }
}

[Table("Clients")]
public class Client
{
    [Key]
    public int ClientID { get; set; }

    public int? UserID { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }

    public ICollection<Animal> Animals { get; set; } = new List<Animal>();

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

[Table("Animals")]
public class Animal
{
    [Key]
    public int AnimalID { get; set; }

    public int ClientID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Species { get; set; } = string.Empty;

    public string? Breed { get; set; }

    public string Gender { get; set; } = "Unknown";

    public DateTime? DateOfBirth { get; set; }

    public decimal? WeightKg { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public Client Client { get; set; } = null!;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

[Table("Appointments")]
public class Appointment
{
    [Key]
    public int AppointmentID { get; set; }

    public int AnimalID { get; set; }

    public int? ClientID { get; set; }

    public int? ServiceID { get; set; }

    public int VeterinarianID { get; set; }

    public DateTime AppointmentTime { get; set; }

    public int ExpectedDurationMinutes { get; set; } = 30;

    public string? Reason { get; set; }

    public string Status { get; set; } = AppointmentStatuses.Pending;

    public int? CreatedByUserID { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CancelledAt { get; set; }

    public Animal Animal { get; set; } = null!;

    public Client? Client { get; set; }

    public ClinicService? Service { get; set; }

    public User Veterinarian { get; set; } = null!;

    public User? CreatedBy { get; set; }

    public Visit? Visit { get; set; }
}

[Table("Visits")]
public class Visit
{
    [Key]
    public int VisitID { get; set; }

    public int AppointmentID { get; set; }

    public DateTime CheckInAt { get; set; } = DateTime.UtcNow;

    public int ReceptionistID { get; set; }

    public string Status { get; set; } = VisitStatuses.Waiting;

    public DateTime? CompletedAt { get; set; }

    public Appointment Appointment { get; set; } = null!;

    public User Receptionist { get; set; } = null!;

    public QueueEntry? QueueEntry { get; set; }
}

[Table("QueueEntries")]
public class QueueEntry
{
    [Key]
    public int QueueEntryID { get; set; }

    public int VisitID { get; set; }

    public DateTime QueueDate { get; set; }

    public int QueueNumber { get; set; }

    public string Status { get; set; } = QueueStatuses.Waiting;

    public DateTime? CalledAt { get; set; }

    public Visit Visit { get; set; } = null!;
}

[Table("ClinicSettings")]
public class ClinicSetting
{
    [Key]
    public int ClinicSettingID { get; set; }

    public string ClinicName { get; set; } = "Phòng khám thú y VCMS";

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public int SlotMinutes { get; set; } = 30;

    public int OpenHour { get; set; } = 8;

    public int CloseHour { get; set; } = 17;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("Services")]
public class ClinicService
{
    [Key]
    public int ServiceID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = ServiceCategories.Veterinary;

    public int DurationMinutes { get; set; } = 30;

    public decimal Price { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

[Table("StaffSchedules")]
public class StaffSchedule
{
    [Key]
    public int StaffScheduleID { get; set; }

    public int UserID { get; set; }

    public byte DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsWorking { get; set; } = true;

    public User User { get; set; } = null!;
}

[Table("BlockedTimes")]
public class BlockedTime
{
    [Key]
    public int BlockedTimeID { get; set; }

    public int UserID { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public string? Reason { get; set; }

    public User User { get; set; } = null!;
}
