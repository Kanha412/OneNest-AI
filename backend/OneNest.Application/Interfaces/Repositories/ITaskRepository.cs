using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface ITaskRepository
{
    Task<List<TaskItem>> GetAllAsync(Guid userId);

    Task<TaskItem?> GetByIdAsync(Guid id, Guid userId);

    Task AddAsync(TaskItem task);

    Task UpdateAsync(TaskItem task);

    Task DeleteAsync(TaskItem task);
}