using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class NoteRepository : INoteRepository
{
    private readonly OneNestDbContext _dbContext;

    public NoteRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Note>> GetAllAsync(Guid userId)
    {
        return await _dbContext.Notes
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Note?> GetByIdAsync(Guid id, Guid userId)
{
    return await _dbContext.Notes
        .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
}

    public async Task AddAsync(Note note)
    {
        _dbContext.Notes.Add(note);
        await _dbContext.SaveChangesAsync();
    }

 public async Task UpdateAsync(Note note)
{
    _dbContext.Notes.Update(note);
    await _dbContext.SaveChangesAsync();
}

    public async Task DeleteAsync(Note note)
    {
        _dbContext.Notes.Remove(note);
        await _dbContext.SaveChangesAsync();
    }
}