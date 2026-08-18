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
    public DbSet<AIConversation> AIConversations => Set<AIConversation>();
    public DbSet<AIMessage> AIMessages => Set<AIMessage>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

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

        modelBuilder.Entity<AIConversation>()
            .HasIndex(x => x.UserId);

        modelBuilder.Entity<AIConversation>()
            .HasIndex(x => x.CreatedAt);

        modelBuilder.Entity<AIConversation>()
            .HasIndex(x => x.UpdatedAt);

        modelBuilder.Entity<AIConversation>()
            .HasIndex(x => x.LastMessageAt);

        modelBuilder.Entity<AIMessage>()
            .HasIndex(x => x.ConversationId);

        modelBuilder.Entity<AIMessage>()
            .HasIndex(x => x.CreatedAt);

        modelBuilder.Entity<AIConversation>()
            .HasMany(x => x.Messages)
            .WithOne(x => x.Conversation)
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserSettings>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        modelBuilder.Entity<UserSettings>()
            .Property(x => x.AutoDeleteTrashDays)
            .HasDefaultValue(30);

        modelBuilder.Entity<UserSettings>()
            .Property(x => x.ReminderLeadTimeHours)
            .HasDefaultValue(24);
    }
}
