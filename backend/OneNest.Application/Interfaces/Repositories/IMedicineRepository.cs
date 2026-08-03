using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface IMedicineRepository
{
    Task<List<Medicine>> GetAllAsync(Guid userId);

    Task<Medicine?> GetByIdAsync(Guid id, Guid userId);

    Task AddAsync(Medicine medicine);

    Task UpdateAsync(Medicine medicine);

    Task DeleteAsync(Medicine medicine);
}
