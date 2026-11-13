using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Prn222_Group3.Models;

[Table("Roles")]
public class Role
{
    [Key]
    [Column(TypeName = "int")]
    public int RoleID { get; set; }

    [Column(TypeName = "varchar(40)")]
    [Required]
    [StringLength(40)]
    public string RoleName { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(200)")]
    [StringLength(200)]
    public string? Description { get; set; }

}

[Table("Users")]
public class User
{
    [Key]
    [Column(TypeName = "int")]
    public int UserID { get; set; }

    [Column(TypeName = "int")]
    public int RoleID { get; set; }

    [Column(TypeName = "varchar(80)")]
    [Required]
    [StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(255)")]
    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(120)")]
    [Required]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Column(TypeName = "varchar(150)")]
    [StringLength(150)]
    public string? Email { get; set; }

    [Column(TypeName = "varchar(20)")]
    [StringLength(20)]
    public string? Phone { get; set; }

    [Column(TypeName = "bit")]
    public bool IsActive { get; set; } = true;

    [Column(TypeName = "datetime2(0)")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;
    public Client? Client { get; set; }
}

[Table("Clients")]
public class Client
{
    [Key]
    [Column(TypeName = "int")]
    public int ClientID { get; set; }

    [Column(TypeName = "int")]
    public int? UserID { get; set; }

    [Column(TypeName = "nvarchar(120)")]
    [Required]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Column(TypeName = "varchar(20)")]
    [Required]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Column(TypeName = "varchar(150)")]
    [StringLength(150)]
    public string? Email { get; set; }

    [Column(TypeName = "nvarchar(255)")]
    [StringLength(255)]
    public string? Address { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public ICollection<Animal> Animals { get; set; } = new List<Animal>();
}

[Table("Animals")]
public class Animal
{
    [Key]
    [Column(TypeName = "int")]
    public int AnimalID { get; set; }

    [Column(TypeName = "int")]
    public int ClientID { get; set; }

    [Column(TypeName = "nvarchar(100)")]
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(60)")]
    [Required]
    [StringLength(60)]
    public string Species { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(100)")]
    [StringLength(100)]
    public string? Breed { get; set; }

    [Column(TypeName = "varchar(10)")]
    [Required]
    [StringLength(10)]
    public string Gender { get; set; } = "Unknown";

    [Column(TypeName = "date")]
    public DateTime? DateOfBirth { get; set; }

    [Column(TypeName = "decimal(7,2)")]
    public decimal? WeightKg { get; set; }

    [Column(TypeName = "nvarchar(500)")]
    [StringLength(500)]
    public string? Notes { get; set; }

    [Column(TypeName = "bit")]
    public bool IsActive { get; set; } = true;

    public Client Client { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

[Table("Appointments")]
public class Appointment
{
    [Key]
    [Column(TypeName = "int")]
    public int AppointmentID { get; set; }

    [Column(TypeName = "int")]
    public int AnimalID { get; set; }

    [Column(TypeName = "int")]
    public int VeterinarianID { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime AppointmentTime { get; set; }

    [Column(TypeName = "int")]
    public int ExpectedDurationMinutes { get; set; } = 30;

    [Column(TypeName = "nvarchar(500)")]
    [StringLength(500)]
    public string? Reason { get; set; }

    [Column(TypeName = "varchar(20)")]
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending";

    [Column(TypeName = "int")]
    public int? CreatedByUserID { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "datetime2(0)")]
    public DateTime? CancelledAt { get; set; }

    public Animal Animal { get; set; } = null!;
    [ForeignKey(nameof(VeterinarianID))]
    public User Veterinarian { get; set; } = null!;
    [ForeignKey(nameof(CreatedByUserID))]
    public User? CreatedBy { get; set; }
    public Visit? Visit { get; set; }
}

[Table("Visits")]
public class Visit
{
    [Key]
    [Column(TypeName = "int")]
    public int VisitID { get; set; }

    [Column(TypeName = "int")]
    public int AppointmentID { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime CheckInAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "int")]
    public int ReceptionistID { get; set; }

    [Column(TypeName = "varchar(20)")]
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Waiting";

    [Column(TypeName = "datetime2(0)")]
    public DateTime? CompletedAt { get; set; }

    public Appointment Appointment { get; set; } = null!;
    [ForeignKey(nameof(ReceptionistID))]
    public User Receptionist { get; set; } = null!;
    public QueueEntry? QueueEntry { get; set; }
    public Encounter? Encounter { get; set; }
}

[Table("QueueEntries")]
public class QueueEntry
{
    [Key]
    [Column(TypeName = "int")]
    public int QueueEntryID { get; set; }

    [Column(TypeName = "int")]
    public int VisitID { get; set; }

    [Column(TypeName = "date")]
    public DateTime QueueDate { get; set; }

    [Column(TypeName = "int")]
    public int QueueNumber { get; set; }

    [Column(TypeName = "varchar(20)")]
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Waiting";

    [Column(TypeName = "datetime2(0)")]
    public DateTime? CalledAt { get; set; }

    public Visit Visit { get; set; } = null!;
}

[Table("Encounters")]
public class Encounter
{
    [Key]
    [Column(TypeName = "int")]
    public int EncounterID { get; set; }

    [Column(TypeName = "int")]
    public int VisitID { get; set; }

    [Column(TypeName = "int")]
    public int VeterinarianID { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Symptoms { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ExaminationNotes { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Diagnosis { get; set; }

    [Column(TypeName = "varchar(20)")]
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Draft";

    [Column(TypeName = "datetime2(0)")]
    public DateTime ExaminedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "datetime2(0)")]
    public DateTime? FinalizedAt { get; set; }

    public Visit Visit { get; set; } = null!;
    [ForeignKey(nameof(VeterinarianID))]
    public User Veterinarian { get; set; } = null!;
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}

[Table("Prescriptions")]
public class Prescription
{
    [Key]
    [Column(TypeName = "int")]
    public int PrescriptionID { get; set; }

    [Column(TypeName = "int")]
    public int EncounterID { get; set; }

    [Column(TypeName = "int")]
    public int PrescribedByUserID { get; set; }

    [Column(TypeName = "varchar(20)")]
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Draft";

    [Column(TypeName = "nvarchar(1000)")]
    [StringLength(1000)]
    public string? Instructions { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "datetime2(0)")]
    public DateTime? FinalizedAt { get; set; }

    public Encounter Encounter { get; set; } = null!;
    [ForeignKey(nameof(PrescribedByUserID))]
    public User PrescribedBy { get; set; } = null!;
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    public ICollection<Dispensation> Dispensations { get; set; } = new List<Dispensation>();
}

[Table("Services")]
public class ClinicService
{
    [Key]
    [Column(TypeName = "int")]
    public int ServiceID { get; set; }

    [Column(TypeName = "nvarchar(150)")]
    [Required]
    [StringLength(150)]
    public string ServiceName { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(500)")]
    [StringLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentPrice { get; set; }

    [Column(TypeName = "bit")]
    public bool IsActive { get; set; } = true;

}

[Table("VeterinarianSchedules")]
public class VeterinarianSchedule
{
    [Key]
    [Column(TypeName = "int")]
    public int ScheduleID { get; set; }

    [Column(TypeName = "int")]
    public int VeterinarianID { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime StartAt { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime EndAt { get; set; }

    [Column(TypeName = "bit")]
    public bool IsAvailable { get; set; } = true;

    [Column(TypeName = "nvarchar(255)")]
    [StringLength(255)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(VeterinarianID))]
    public User Veterinarian { get; set; } = null!;
}

[Table("Medicines")]
public class Medicine
{
    [Key]
    [Column(TypeName = "int")]
    public int MedicineID { get; set; }

    [Column(TypeName = "nvarchar(150)")]
    [Required]
    [StringLength(150)]
    public string MedicineName { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(30)")]
    [Required]
    [StringLength(30)]
    public string Unit { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentPrice { get; set; }

    [Column(TypeName = "bit")]
    public bool IsActive { get; set; } = true;

    public ICollection<MedicineBatch> Batches { get; set; } = new List<MedicineBatch>();
}

[Table("MedicineBatches")]
public class MedicineBatch
{
    [Key]
    [Column(TypeName = "int")]
    public int BatchID { get; set; }

    [Column(TypeName = "int")]
    public int MedicineID { get; set; }

    [Column(TypeName = "varchar(80)")]
    [Required]
    [StringLength(80)]
    public string BatchNumber { get; set; } = string.Empty;

    [Column(TypeName = "date")]
    public DateTime ExpiryDate { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "int")]
    public int QuantityOnHand { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PurchaseUnitCost { get; set; }

    public Medicine Medicine { get; set; } = null!;
}

[Table("PrescriptionItems")]
public class PrescriptionItem
{
    [Key]
    [Column(TypeName = "int")]
    public int PrescriptionItemID { get; set; }

    [Column(TypeName = "int")]
    public int PrescriptionID { get; set; }

    [Column(TypeName = "int")]
    public int MedicineID { get; set; }

    [Column(TypeName = "int")]
    public int Quantity { get; set; }

    [Column(TypeName = "nvarchar(200)")]
    [Required]
    [StringLength(200)]
    public string Dosage { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(200)")]
    [StringLength(200)]
    public string? Frequency { get; set; }

    [Column(TypeName = "int")]
    public int? DurationDays { get; set; }

    [Column(TypeName = "nvarchar(500)")]
    [StringLength(500)]
    public string? Instructions { get; set; }

    public Prescription Prescription { get; set; } = null!;
    public Medicine Medicine { get; set; } = null!;
}

[Table("Dispensations")]
public class Dispensation
{
    [Key]
    [Column(TypeName = "int")]
    public int DispensationID { get; set; }

    [Column(TypeName = "int")]
    public int PrescriptionID { get; set; }

    [Column(TypeName = "int")]
    public int DispensedByUserID { get; set; }

    [Column(TypeName = "varchar(20)")]
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Draft";

    [Column(TypeName = "datetime2(0)")]
    public DateTime? DispensedAt { get; set; }

    [Column(TypeName = "uniqueidentifier")]
    public Guid RequestKey { get; set; } = Guid.NewGuid();

    public Prescription Prescription { get; set; } = null!;
    [ForeignKey(nameof(DispensedByUserID))]
    public User DispensedBy { get; set; } = null!;
    public ICollection<DispensationItem> Items { get; set; } = new List<DispensationItem>();
}

[Table("DispensationItems")]
public class DispensationItem
{
    [Key]
    [Column(TypeName = "int")]
    public int DispensationItemID { get; set; }

    [Column(TypeName = "int")]
    public int DispensationID { get; set; }

    [Column(TypeName = "int")]
    public int PrescriptionItemID { get; set; }

    [Column(TypeName = "int")]
    public int BatchID { get; set; }

    [Column(TypeName = "int")]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    public Dispensation Dispensation { get; set; } = null!;
    public PrescriptionItem PrescriptionItem { get; set; } = null!;
    [ForeignKey(nameof(BatchID))]
    public MedicineBatch Batch { get; set; } = null!;
}

[Table("StockMovements")]
public class StockMovement
{
    [Key]
    [Column(TypeName = "int")]
    public int StockMovementID { get; set; }

    [Column(TypeName = "int")]
    public int BatchID { get; set; }

    [Column(TypeName = "int")]
    public int? DispensationItemID { get; set; }

    [Column(TypeName = "varchar(20)")]
    [Required]
    [StringLength(20)]
    public string MovementType { get; set; } = string.Empty;

    [Column(TypeName = "int")]
    public int QuantityChange { get; set; }

    [Column(TypeName = "nvarchar(500)")]
    [StringLength(500)]
    public string? Reason { get; set; }

    [Column(TypeName = "int")]
    public int PerformedByUserID { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(BatchID))]
    public MedicineBatch Batch { get; set; } = null!;
    public DispensationItem? DispensationItem { get; set; }
    [ForeignKey(nameof(PerformedByUserID))]
    public User PerformedBy { get; set; } = null!;
}

[Table("AuditLogs")]
public class AuditLog
{
    [Key]
    [Column(TypeName = "bigint")]
    public long AuditLogID { get; set; }

    [Column(TypeName = "int")]
    public int? ActorUserID { get; set; }

    [Column(TypeName = "varchar(100)")]
    [Required]
    [StringLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [Column(TypeName = "varchar(80)")]
    [Required]
    [StringLength(80)]
    public string EntityID { get; set; } = string.Empty;

    [Column(TypeName = "varchar(40)")]
    [Required]
    [StringLength(40)]
    public string Action { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string? OldValues { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? NewValues { get; set; }

    [Column(TypeName = "bigint")]
    public long? PreviousAuditLogID { get; set; }

    [Column(TypeName = "datetime2(0)")]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

}
