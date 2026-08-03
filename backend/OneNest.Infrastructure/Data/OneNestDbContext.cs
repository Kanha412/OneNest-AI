using Microsoft.EntityFrameworkCore;
using OneNest.Domain.Entities;

namespace OneNest.Infrastructure.Data;

public class OneNestDbContext : DbContext
{
    public OneNestDbContext(DbContextOptions<OneNestDbContext> options)
        : base(options)
    {
    }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<MedicalReport> MedicalReports => Set<MedicalReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<Expense>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<MedicalRecord>()
            .Property(x => x.HeightCm)
            .HasPrecision(6, 2);

        modelBuilder.Entity<MedicalRecord>()
            .Property(x => x.WeightKg)
            .HasPrecision(6, 2);
    }
}