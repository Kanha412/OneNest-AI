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
}