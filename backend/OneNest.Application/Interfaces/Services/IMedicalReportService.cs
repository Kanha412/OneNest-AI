using OneNest.Application.DTOs.Health;
using OneNest.Domain.Enums;

namespace OneNest.Application.Interfaces.Services;

public interface IMedicalReportService
{
    Task<List<MedicalReportResponse>> GetAllAsync(string? search, MedicalReportCategory? category);

    Task<MedicalReportResponse?> GetByIdAsync(Guid id);

    Task<MedicalReportResponse> UploadAsync(UploadMedicalReportInput input);

    Task<MedicalReportResponse?> UpdateAsync(Guid id, UpdateMedicalReportRequest request);

    Task<bool> DeleteAsync(Guid id);

    Task<MedicalReportFileResult?> DownloadAsync(Guid id);
}
