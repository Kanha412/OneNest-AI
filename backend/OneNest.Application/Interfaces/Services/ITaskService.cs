using OneNest.Application.DTOs.Tasks;

namespace OneNest.Application.Interfaces.Services;

public interface ITaskService
{
    Task<List<TaskResponse>> GetAllAsync();

    Task<TaskResponse> CreateAsync(CreateTaskRequest request);

    Task<TaskResponse?> UpdateAsync(Guid id, UpdateTaskRequest request);

    Task DeleteAsync(Guid id);

    Task ToggleCompleteAsync(Guid id);
}