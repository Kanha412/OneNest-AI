using OneNest.Domain.Entities;
using OneNest.Domain.Enums;

namespace OneNest.Application.Interfaces.Repositories;

public interface IMedicalReportRepository
{
    Task<List<MedicalReport>> GetAllAsync(Guid userId);

    Task<MedicalReport?> GetByIdAsync(Guid id, Guid userId);

    Task AddAsync(MedicalReport report);

    Task UpdateAsync(MedicalReport report);

    Task DeleteAsync(MedicalReport report);

    Task<List<MedicalReport>> SearchAsync(Guid userId, string searchTerm);

    Task<List<MedicalReport>> GetByCategoryAsync(Guid userId, MedicalReportCategory category);

    Task<List<MedicalReport>> GetRecentAsync(Guid userId, int count);
}
