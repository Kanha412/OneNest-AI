using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface IContactRepository
{
    Task<List<ContactMessage>> GetAllAsync();
    Task<List<ContactMessage>> GetByUserIdAsync(Guid userId);
    Task<ContactMessage?> GetByIdAsync(Guid id);
    Task AddAsync(ContactMessage message);
    Task UpdateAsync(ContactMessage message);
}
