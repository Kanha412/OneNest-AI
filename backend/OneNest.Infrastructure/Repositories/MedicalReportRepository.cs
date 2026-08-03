using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class MedicalReportRepository : IMedicalReportRepository
{
    private readonly OneNestDbContext _dbContext;

    public MedicalReportRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<MedicalReport>> GetAllAsync(Guid userId)
    {
        return await _dbContext.MedicalReports
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.ReportDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<MedicalReport?> GetByIdAsync(Guid id, Guid userId)
    {
        return await _dbContext.MedicalReports
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    public async Task AddAsync(MedicalReport report)
    {
        _dbContext.MedicalReports.Add(report);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(MedicalReport report)
    {
        _dbContext.MedicalReports.Update(report);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(MedicalReport report)
    {
        _dbContext.MedicalReports.Remove(report);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<MedicalReport>> SearchAsync(Guid userId, string searchTerm)
    {
        var term = searchTerm.Trim().ToLower();

        return await _dbContext.MedicalReports
            .Where(x => x.UserId == userId &&
                (x.Title.ToLower().Contains(term) ||
                 x.DoctorName.ToLower().Contains(term) ||
                 x.Hospital.ToLower().Contains(term) ||
                 x.Description.ToLower().Contains(term)))
            .OrderByDescending(x => x.ReportDate)
            .ToListAsync();
    }

    public async Task<List<MedicalReport>> GetByCategoryAsync(Guid userId, MedicalReportCategory category)
    {
        return await _dbContext.MedicalReports
            .Where(x => x.UserId == userId && x.Category == category)
            .OrderByDescending(x => x.ReportDate)
            .ToListAsync();
    }

    public async Task<List<MedicalReport>> GetRecentAsync(Guid userId, int count)
    {
        return await _dbContext.MedicalReports
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync();
    }
}
