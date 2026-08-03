using OneNest.Application.DTOs.Health;

namespace OneNest.Application.Interfaces.Services;

public interface IMedicalRecordService
{
    Task<MedicalRecordResponse?> GetAsync();

    Task<MedicalRecordResponse> SaveAsync(SaveMedicalRecordRequest request);
}
