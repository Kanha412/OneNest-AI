using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class ContactRepository : IContactRepository
{
    private readonly OneNestDbContext _dbContext;

    public ContactRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ContactMessage>> GetAllAsync()
    {
        return await _dbContext.ContactMessages
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<ContactMessage>> GetByUserIdAsync(Guid userId)
    {
        return await _dbContext.ContactMessages
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<ContactMessage?> GetByIdAsync(Guid id)
    {
        return await _dbContext.ContactMessages
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task AddAsync(ContactMessage message)
    {
        _dbContext.ContactMessages.Add(message);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(ContactMessage message)
    {
        _dbContext.ContactMessages.Update(message);
        await _dbContext.SaveChangesAsync();
    }
}
