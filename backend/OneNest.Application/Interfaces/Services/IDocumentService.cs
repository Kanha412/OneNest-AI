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

    Task<int> DeleteAllAsync();

    Task<DocumentFileResult?> DownloadAsync(Guid id);

    Task<DocumentFileResult?> DownloadAllAsync();

    Task<List<DocumentResponse>> GetRecentAsync(int count);

    Task<DocumentSummaryResponse> GetSummaryAsync();

    // Phase 6 — AI Document Intelligence
    Task<string?> GetExtractedTextAsync(Guid id);

    Task<DocumentResponse?> SummarizeAsync(Guid id, CancellationToken cancellationToken = default);
}
