using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Documents;

public class CategoryDistributionResponse
{
    public DocumentCategory Category { get; set; }

    public int Count { get; set; }
}

public class DocumentSummaryResponse
{
    public int TotalDocuments { get; set; }

    public int TodayUploads { get; set; }

    public long StorageUsed { get; set; }

    public List<DocumentResponse> RecentDocuments { get; set; } = new();

    public List<CategoryDistributionResponse> CategoryDistribution { get; set; } = new();
}
