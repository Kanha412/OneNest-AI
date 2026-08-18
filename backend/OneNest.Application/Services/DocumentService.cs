using System.IO.Compression;
using OneNest.Application.DTOs.AI;
using OneNest.Application.DTOs.Documents;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Application.Interfaces.Storage;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;

namespace OneNest.Application.Services;

public class DocumentService : IDocumentService
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB
    private const long MaxTotalStorageBytes = 150L * 1024 * 1024; // 150 MB
    private const int RecentCount = 4;
    private const string ZipContentType = "application/zip";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".rtf",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
    };

    private readonly IDocumentRepository _documentRepository;
    private readonly IMedicalReportRepository _medicalReportRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly IAIProvider _aiProvider;
    private readonly ISemanticIndexService _semanticIndexService;

    public DocumentService(
        IDocumentRepository documentRepository,
        IMedicalReportRepository medicalReportRepository,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        IDocumentTextExtractor textExtractor,
        IAIProvider aiProvider,
        ISemanticIndexService semanticIndexService)
    {
        _documentRepository = documentRepository;
        _medicalReportRepository = medicalReportRepository;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _textExtractor = textExtractor;
        _aiProvider = aiProvider;
        _semanticIndexService = semanticIndexService;
    }

    public async Task<List<DocumentResponse>> GetAllAsync(string? search, DocumentCategory? category)
    {
        var userId = _currentUserService.UserId;

        List<Document> documents;

        if (!string.IsNullOrWhiteSpace(search))
        {
            documents = await _documentRepository.SearchAsync(userId, search);
        }
        else if (category.HasValue)
        {
            documents = await _documentRepository.GetByCategoryAsync(userId, category.Value);
        }
        else
        {
            documents = await _documentRepository.GetAllAsync(userId);
        }

        if (!string.IsNullOrWhiteSpace(search) && category.HasValue)
        {
            documents = documents.Where(x => x.Category == category.Value).ToList();
        }

        return documents.Select(MapToResponse).ToList();
    }

    public async Task<DocumentResponse?> GetByIdAsync(Guid id)
    {
        var document = await _documentRepository.GetByIdAsync(id, _currentUserService.UserId);

        return document is null ? null : MapToResponse(document);
    }

    public async Task<DocumentResponse> UploadAsync(UploadDocumentInput input)
    {
        var userId = _currentUserService.UserId;

        if (input.Content is null || input.FileSize <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        if (input.FileSize > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("File size exceeds the 25 MB limit.");
        }

        var documents = await _documentRepository.GetAllAsync(userId);
        var medicalReports = await _medicalReportRepository.GetAllAsync(userId);
        var currentUsageBytes = documents.Sum(x => x.FileSize) + medicalReports.Sum(x => x.FileSize);
        var projectedUsageBytes = currentUsageBytes + input.FileSize;

        if (projectedUsageBytes > MaxTotalStorageBytes)
        {
            throw new InvalidOperationException($"Upload exceeds your 150 MB storage limit. Current usage: {FormatSize(currentUsageBytes)} / 150 MB.");
        }

        var extension = Path.GetExtension(input.OriginalFileName);

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"File type '{extension}' is not allowed.");
        }

        if (await _documentRepository.ExistsByOriginalFileNameAsync(userId, input.OriginalFileName))
        {
            throw new InvalidOperationException($"A document named '{input.OriginalFileName}' already exists.");
        }

        var storedFileName = $"{Guid.NewGuid()}{extension}";

        await _fileStorageService.SaveAsync(userId, storedFileName, input.Content);

        // Phase 6 — extract text from the saved file (best-effort, never blocks upload)
        string? extractedText = null;
        bool isTextExtracted = false;
        DateTime? textExtractedAt = null;

        if (_textExtractor.CanExtract(extension))
        {
            try
            {
                await using var readStream = await _fileStorageService.OpenReadAsync(userId, storedFileName);
                if (readStream is not null)
                {
                    extractedText = await _textExtractor.ExtractAsync(readStream, extension);
                    isTextExtracted = true;
                    textExtractedAt = DateTime.UtcNow;
                }
            }
            catch
            {
                // Extraction is best-effort — upload still succeeds on extractor failure
            }
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(input.Metadata.Title)
                ? Path.GetFileNameWithoutExtension(input.OriginalFileName)
                : input.Metadata.Title,
            OriginalFileName = input.OriginalFileName,
            StoredFileName = storedFileName,
            ContentType = input.ContentType,
            FileSize = input.FileSize,
            Category = input.Metadata.Category,
            Description = input.Metadata.Description,
            ExtractedText = extractedText,
            IsTextExtracted = isTextExtracted,
            TextExtractedAt = textExtractedAt,
            CreatedAt = DateTime.UtcNow
        };

        await _documentRepository.AddAsync(document);

        // Phase 8 — best-effort semantic indexing; never blocks upload
        if (!string.IsNullOrWhiteSpace(extractedText))
        {
            await _semanticIndexService.IndexAsync(
                userId, EmbeddingSourceType.Document, document.Id,
                document.Title, extractedText);
        }

        return MapToResponse(document);
    }

    public async Task<DocumentResponse?> UpdateAsync(Guid id, UpdateDocumentRequest request)
    {
        var document = await _documentRepository.GetByIdAsync(id, _currentUserService.UserId);

        if (document is null)
            return null;

        document.Title = request.Title;
        document.Category = request.Category;
        document.Description = request.Description;
        document.UpdatedAt = DateTime.UtcNow;

        await _documentRepository.UpdateAsync(document);

        return MapToResponse(document);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var document = await _documentRepository.GetByIdAsync(id, userId);

        if (document is null)
            return false;

        await _fileStorageService.DeleteAsync(userId, document.StoredFileName);
        await _documentRepository.DeleteAsync(document);

        // Phase 8 — remove embedding entry
        await _semanticIndexService.DeleteIndexAsync(userId, EmbeddingSourceType.Document, id);

        return true;
    }

    public async Task<DocumentFileResult?> DownloadAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var document = await _documentRepository.GetByIdAsync(id, userId);

        if (document is null)
            return null;

        var stream = await _fileStorageService.OpenReadAsync(userId, document.StoredFileName);

        if (stream is null)
            return null;

        return new DocumentFileResult
        {
            Content = stream,
            ContentType = string.IsNullOrWhiteSpace(document.ContentType)
                ? "application/octet-stream"
                : document.ContentType,
            OriginalFileName = document.OriginalFileName
        };
    }

    public async Task<DocumentFileResult?> DownloadAllAsync()
    {
        var userId = _currentUserService.UserId;
        var documents = await _documentRepository.GetAllAsync(userId);
        var reports = await _medicalReportRepository.GetAllAsync(userId);

        if (!documents.Any() && !reports.Any())
            return null;

        var archiveStream = new MemoryStream();

        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var document in documents)
            {
                await using var sourceStream = await _fileStorageService.OpenReadAsync(userId, document.StoredFileName);
                if (sourceStream is null)
                    continue;

                var safeName = string.IsNullOrWhiteSpace(document.OriginalFileName)
                    ? $"document-{document.Id}"
                    : document.OriginalFileName;

                var entry = archive.CreateEntry($"Documents/{safeName}", CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await sourceStream.CopyToAsync(entryStream);
            }

            foreach (var report in reports)
            {
                await using var sourceStream = await _fileStorageService.OpenReadAsync(userId, report.StoredFileName);
                if (sourceStream is null)
                    continue;

                var safeName = string.IsNullOrWhiteSpace(report.OriginalFileName)
                    ? $"report-{report.Id}"
                    : report.OriginalFileName;

                var entry = archive.CreateEntry($"Health-Reports/{safeName}", CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await sourceStream.CopyToAsync(entryStream);
            }
        }

        archiveStream.Position = 0;

        return new DocumentFileResult
        {
            Content = archiveStream,
            ContentType = ZipContentType,
            OriginalFileName = $"onenest-storage-{DateTime.UtcNow:yyyyMMddHHmmss}.zip"
        };
    }

    public async Task<int> DeleteAllAsync()
    {
        var userId = _currentUserService.UserId;
        var documents = await _documentRepository.GetAllAsync(userId);
        var reports = await _medicalReportRepository.GetAllAsync(userId);

        foreach (var document in documents)
        {
            await _fileStorageService.DeleteAsync(userId, document.StoredFileName);
            await _documentRepository.DeleteAsync(document);
        }

        foreach (var report in reports)
        {
            await _fileStorageService.DeleteAsync(userId, report.StoredFileName);
            await _medicalReportRepository.DeleteAsync(report);
        }

        // Phase 8 — remove all document embedding records for the user.
        // Medical reports are not indexed, so only Document-type embeddings need cleanup.
        if (documents.Count > 0)
        {
            await _semanticIndexService.DeleteAllBySourceTypeAsync(
                userId, EmbeddingSourceType.Document);
        }

        return documents.Count + reports.Count;
    }

    public async Task<List<DocumentResponse>> GetRecentAsync(int count)
    {
        var documents = await _documentRepository.GetRecentAsync(
            _currentUserService.UserId,
            count <= 0 ? RecentCount : count);

        return documents.Select(MapToResponse).ToList();
    }

    public async Task<DocumentSummaryResponse> GetSummaryAsync()
    {
        var documents = await _documentRepository.GetAllAsync(_currentUserService.UserId);

        var today = DateTime.UtcNow.Date;

        return new DocumentSummaryResponse
        {
            TotalDocuments = documents.Count,
            TodayUploads = documents.Count(x => x.CreatedAt.Date == today),
            StorageUsed = documents.Sum(x => x.FileSize),
            RecentDocuments = documents
                .OrderByDescending(x => x.CreatedAt)
                .Take(RecentCount)
                .Select(MapToResponse)
                .ToList(),
            CategoryDistribution = documents
                .GroupBy(x => x.Category)
                .Select(g => new CategoryDistributionResponse
                {
                    Category = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList()
        };
    }

    // ── Phase 6 — AI Document Intelligence ────────────────────────────────

    public async Task<string?> GetExtractedTextAsync(Guid id)
    {
        var document = await _documentRepository.GetByIdAsync(id, _currentUserService.UserId);
        return document?.ExtractedText;
    }

    public async Task<DocumentResponse?> SummarizeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(id, _currentUserService.UserId);

        if (document is null)
            return null;

        if (string.IsNullOrWhiteSpace(document.ExtractedText))
            throw new InvalidOperationException("No extracted text is available for this document. Text extraction may not be supported for its file type.");

        var systemPrompt =
            "You are a document summarization assistant. " +
            "Produce a concise summary (3-5 sentences) of the document text provided. " +
            "Focus on the key points. Respond with the summary only — no preamble.";

        var userMessage = new ConversationMessage
        {
            Role = "user",
            Content = $"Document title: {document.Title}\n\nDocument text:\n{document.ExtractedText[..Math.Min(document.ExtractedText.Length, 8000)]}"
        };

        var aiSummary = await _aiProvider.GenerateResponseAsync(
            systemPrompt,
            new List<ConversationMessage> { userMessage },
            cancellationToken);

        document.AISummary = aiSummary?.Trim();
        document.AISummarizedAt = DateTime.UtcNow;
        document.UpdatedAt = DateTime.UtcNow;

        await _documentRepository.UpdateAsync(document);

        return MapToResponse(document);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static DocumentResponse MapToResponse(Document document)
    {
        return new DocumentResponse
        {
            Id = document.Id,
            Title = document.Title,
            OriginalFileName = document.OriginalFileName,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            Category = document.Category,
            Description = document.Description,
            IsTextExtracted = document.IsTextExtracted,
            AISummary = document.AISummary,
            AISummarizedAt = document.AISummarizedAt,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };
    }

    private static string FormatSize(long bytes)
    {
        var valueInMb = bytes / (1024d * 1024d);
        return $"{valueInMb:0.##} MB";
    }
}
