using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class MedicalRecordRepository : IMedicalRecordRepository
{
    private readonly OneNestDbContext _dbContext;

    public MedicalRecordRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MedicalRecord?> GetByUserAsync(Guid userId)
    {
        return await _dbContext.MedicalRecords
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task AddAsync(MedicalRecord record)
    {
        _dbContext.MedicalRecords.Add(record);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(MedicalRecord record)
    {
        _dbContext.MedicalRecords.Update(record);
        await _dbContext.SaveChangesAsync();
    }
}
