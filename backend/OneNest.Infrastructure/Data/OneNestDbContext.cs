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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<Expense>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);
    }
}