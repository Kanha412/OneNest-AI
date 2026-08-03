namespace OneNest.Application.DTOs.Health;

public class MedicalRecordResponse
{
    public Guid Id { get; set; }

    public string BloodGroup { get; set; } = string.Empty;

    public decimal? HeightCm { get; set; }

    public decimal? WeightKg { get; set; }

    public string Allergies { get; set; } = string.Empty;

    public string ExistingConditions { get; set; } = string.Empty;

    public string EmergencyContactName { get; set; } = string.Empty;

    public string EmergencyContactPhone { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
