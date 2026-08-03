using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface INoteRepository
{
    Task<List<Note>> GetAllAsync(Guid userId);

    Task<Note?> GetByIdAsync(Guid id, Guid userId);

    Task AddAsync(Note note);

    Task UpdateAsync(Note note);

    Task DeleteAsync(Note note);
}