using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface ITaskRepository
{
    Task<List<TaskItem>> GetAllAsync();

    Task<TaskItem?> GetByIdAsync(Guid id);

    Task AddAsync(TaskItem task);

    Task UpdateAsync(TaskItem task);

    Task DeleteAsync(TaskItem task);
}