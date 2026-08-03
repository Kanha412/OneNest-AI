using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Health;

public class MedicalReportResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public MedicalReportCategory Category { get; set; }

    public string DoctorName { get; set; } = string.Empty;

    public string Hospital { get; set; } = string.Empty;

    public DateOnly ReportDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
