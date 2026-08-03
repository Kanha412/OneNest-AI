using OneNest.Application.DTOs.Documents;
using OneNest.Domain.Enums;

namespace OneNest.Application.Interfaces.Services;

public interface IDocumentService
{
    Task<List<DocumentResponse>> GetAllAsync(string? search, DocumentCategory? category);

    Task<DocumentResponse?> GetByIdAsync(Guid id);

    Task<DocumentResponse> UploadAsync(UploadDocumentInput input);

    Task<DocumentResponse?> UpdateAsync(Guid id, UpdateDocumentRequest request);

    Task<bool> DeleteAsync(Guid id);

    Task<DocumentFileResult?> DownloadAsync(Guid id);

    Task<List<DocumentResponse>> GetRecentAsync(int count);

    Task<DocumentSummaryResponse> GetSummaryAsync();
}
