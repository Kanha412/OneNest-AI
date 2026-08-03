using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Documents;

public class DocumentResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DocumentCategory Category { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
