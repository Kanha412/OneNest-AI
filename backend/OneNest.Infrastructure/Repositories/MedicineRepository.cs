using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class MedicineRepository : IMedicineRepository
{
    private readonly OneNestDbContext _dbContext;

    public MedicineRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Medicine>> GetAllAsync(Guid userId)
    {
        return await _dbContext.Medicines
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Medicine?> GetByIdAsync(Guid id, Guid userId)
    {
        return await _dbContext.Medicines
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    public async Task AddAsync(Medicine medicine)
    {
        _dbContext.Medicines.Add(medicine);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Medicine medicine)
    {
        _dbContext.Medicines.Update(medicine);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Medicine medicine)
    {
        _dbContext.Medicines.Remove(medicine);
        await _dbContext.SaveChangesAsync();
    }
}
