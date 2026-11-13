using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Models;
namespace Project_Prn222_Group3.Data;
public class VcmsDbContext(DbContextOptions<VcmsDbContext> options) : DbContext(options)
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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    private void GuardAudit()
    {
        if (ChangeTracker.Entries<AuditLog>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Lịch sử thao tác chỉ được thêm mới.");
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAudit(); return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardAudit(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
