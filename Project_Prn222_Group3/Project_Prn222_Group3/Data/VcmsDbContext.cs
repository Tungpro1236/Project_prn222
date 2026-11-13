using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Models;
namespace Project_Prn222_Group3.Data;
public partial class VcmsDbContext(DbContextOptions<VcmsDbContext> options, IHttpContextAccessor? httpContext = null) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<ClinicService> Services => Set<ClinicService>();
    public DbSet<VeterinarianSchedule> VeterinarianSchedules => Set<VeterinarianSchedule>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<MedicineBatch> MedicineBatches => Set<MedicineBatch>();
    public DbSet<Dispensation> Dispensations => Set<Dispensation>();
    public DbSet<DispensationItem> DispensationItems => Set<DispensationItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillItem> BillItems => Set<BillItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    private void GuardAudit()
    {
        if (ChangeTracker.Entries<AuditLog>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Lịch sử thao tác chỉ được thêm mới.");
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bill>().Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Bill>().Property(x => x.Status).HasColumnType("varchar(20)");
        modelBuilder.Entity<Bill>().Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        modelBuilder.Entity<BillItem>().Property(x => x.Description).HasMaxLength(255);
        modelBuilder.Entity<BillItem>().Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<BillItem>().Property(x => x.LineTotal)
            .HasComputedColumnSql("CONVERT(decimal(18,2), [Quantity] * [UnitPrice])", stored: true);
        modelBuilder.Entity<Payment>().Property(x => x.Amount).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Payment>().Property(x => x.PaidAt).HasColumnType("datetime2(0)");
        modelBuilder.Entity<Payment>().Property(x => x.Status).HasColumnType("varchar(20)");
        modelBuilder.Entity<Payment>().Property(x => x.PaymentMethod).HasColumnType("varchar(20)");
        modelBuilder.Entity<Client>().HasOne(x => x.User).WithOne(x => x.Client).HasForeignKey<Client>(x => x.UserID);
        modelBuilder.Entity<Visit>().HasOne(x => x.Appointment).WithOne(x => x.Visit).HasForeignKey<Visit>(x => x.AppointmentID);
        modelBuilder.Entity<QueueEntry>().HasOne(x => x.Visit).WithOne(x => x.QueueEntry).HasForeignKey<QueueEntry>(x => x.VisitID);
        modelBuilder.Entity<Encounter>().HasOne(x => x.Visit).WithOne(x => x.Encounter).HasForeignKey<Encounter>(x => x.VisitID);
        modelBuilder.Entity<MedicineBatch>().HasKey(x => x.BatchID);
        modelBuilder.Entity<VeterinarianSchedule>().HasKey(x => x.ScheduleID);
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
