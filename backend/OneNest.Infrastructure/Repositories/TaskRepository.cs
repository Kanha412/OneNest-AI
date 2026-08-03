using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly OneNestDbContext _dbContext;

    public TaskRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TaskItem>> GetAllAsync(Guid userId)
    {
        return await _dbContext.Tasks
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.IsCompleted)
            .ThenBy(x => x.DueDate)
            .ToListAsync();
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, Guid userId)
    {
        return await _dbContext.Tasks
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    public async Task AddAsync(TaskItem task)
    {
        _dbContext.Tasks.Add(task);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(TaskItem task)
    {
        _dbContext.Tasks.Update(task);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(TaskItem task)
    {
        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync();
    }
}