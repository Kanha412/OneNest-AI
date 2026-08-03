using OneNest.Domain.Enums;

namespace OneNest.Domain.Entities;

public class MedicalReport
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public MedicalReportCategory Category { get; set; } = MedicalReportCategory.Other;

    public string DoctorName { get; set; } = string.Empty;

    public string Hospital { get; set; } = string.Empty;

    public DateOnly ReportDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
