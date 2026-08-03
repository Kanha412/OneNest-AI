namespace OneNest.Domain.Entities;

public class MedicalRecord
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string BloodGroup { get; set; } = string.Empty;

    public decimal? HeightCm { get; set; }

    public decimal? WeightKg { get; set; }

    public string Allergies { get; set; } = string.Empty;

    public string ExistingConditions { get; set; } = string.Empty;

    public string EmergencyContactName { get; set; } = string.Empty;

    public string EmergencyContactPhone { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
