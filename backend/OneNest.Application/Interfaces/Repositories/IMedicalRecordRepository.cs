using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface IMedicalRecordRepository
{
    Task<MedicalRecord?> GetByUserAsync(Guid userId);

    Task AddAsync(MedicalRecord record);

    Task UpdateAsync(MedicalRecord record);
}
