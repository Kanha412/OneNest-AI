using System.ComponentModel.DataAnnotations;

namespace OneNest.Application.DTOs.Health;

public class SaveMedicalRecordRequest
{
    [MaxLength(10)]
    public string BloodGroup { get; set; } = string.Empty;

    [Range(0, 300)]
    public decimal? HeightCm { get; set; }

    [Range(0, 500)]
    public decimal? WeightKg { get; set; }

    [MaxLength(1000)]
    public string Allergies { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string ExistingConditions { get; set; } = string.Empty;

    [MaxLength(150)]
    public string EmergencyContactName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string EmergencyContactPhone { get; set; } = string.Empty;
}
