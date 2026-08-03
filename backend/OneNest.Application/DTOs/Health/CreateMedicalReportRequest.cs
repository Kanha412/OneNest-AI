using System.ComponentModel.DataAnnotations;
using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Health;

public class CreateMedicalReportRequest
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    public MedicalReportCategory Category { get; set; } = MedicalReportCategory.Other;

    [MaxLength(150)]
    public string DoctorName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string Hospital { get; set; } = string.Empty;

    public DateOnly ReportDate { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
}
