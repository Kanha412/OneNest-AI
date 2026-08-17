using OneNest.Domain.Enums;

namespace OneNest.Domain.Entities;

public class Document
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DocumentCategory Category { get; set; } = DocumentCategory.Other;

    public string Description { get; set; } = string.Empty;

    // Phase 6 — AI Document Intelligence
    public string? ExtractedText { get; set; }

    public bool IsTextExtracted { get; set; }

    public DateTime? TextExtractedAt { get; set; }

    public string? AISummary { get; set; }

    public DateTime? AISummarizedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
