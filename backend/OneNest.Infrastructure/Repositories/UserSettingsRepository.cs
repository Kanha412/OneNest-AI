using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class UserSettingsRepository : IUserSettingsRepository
{
    private readonly OneNestDbContext _dbContext;

    public UserSettingsRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserSettings?> GetByUserIdAsync(Guid userId)
    {
        return await _dbContext.UserSettings
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task AddAsync(UserSettings settings)
    {
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserSettings settings)
    {
        _dbContext.UserSettings.Update(settings);
        await _dbContext.SaveChangesAsync();
    }
}
