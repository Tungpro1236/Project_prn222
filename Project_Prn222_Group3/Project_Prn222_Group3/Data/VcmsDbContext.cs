using Microsoft.EntityFrameworkCore;
using Project_Prn222_Group3.Models;

namespace Project_Prn222_Group3.Data;

public class VcmsDbContext : DbContext
{
    public VcmsDbContext(DbContextOptions<VcmsDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(x => x.RoleName).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.Username).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(150);
            entity.Property(x => x.Phone).HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");

            entity.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(150);
            entity.Property(x => x.Address).HasMaxLength(255);
            entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
            entity.HasIndex(x => x.UserID).IsUnique();

            entity.HasOne(x => x.User)
                .WithOne(x => x.Client)
                .HasForeignKey<Client>(x => x.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Animals)
                .WithOne(x => x.Client)
                .HasForeignKey(x => x.ClientID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Animal>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Species).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Breed).HasMaxLength(100);
            entity.Property(x => x.Gender).HasMaxLength(10).IsRequired();
            entity.Property(x => x.WeightKg).HasPrecision(7, 2);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.Property(x => x.AppointmentTime).HasColumnType("datetime2(0)");
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
            entity.Property(x => x.CancelledAt).HasColumnType("datetime2(0)");

            entity.HasOne(x => x.Animal)
                .WithMany()
                .HasForeignKey(x => x.AnimalID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Veterinarian)
                .WithMany()
                .HasForeignKey(x => x.VeterinarianID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Visit>(entity =>
        {
            entity.Property(x => x.CheckInAt).HasColumnType("datetime2(0)");
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CompletedAt).HasColumnType("datetime2(0)");

            entity.HasOne(x => x.Appointment)
                .WithOne(x => x.Visit)
                .HasForeignKey<Visit>(x => x.AppointmentID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Receptionist)
                .WithMany()
                .HasForeignKey(x => x.ReceptionistID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QueueEntry>(entity =>
        {
            entity.Property(x => x.QueueDate).HasColumnType("date");
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CalledAt).HasColumnType("datetime2(0)");

            entity.HasOne(x => x.Visit)
                .WithOne(x => x.QueueEntry)
                .HasForeignKey<QueueEntry>(x => x.VisitID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
