using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface IUserSettingsRepository
{
    Task<UserSettings?> GetByUserIdAsync(Guid userId);

    Task AddAsync(UserSettings settings);

    Task UpdateAsync(UserSettings settings);
}
