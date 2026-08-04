using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);

    Task<User?> GetByEmailAsync(string email);

    Task<bool> EmailExistsAsync(string email);

    Task AddAsync(User user);

    Task UpdateAsync(User user);
}
