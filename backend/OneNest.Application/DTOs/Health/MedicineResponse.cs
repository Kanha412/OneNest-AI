using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Health;

public class MedicineResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Dosage { get; set; } = string.Empty;

    public string Frequency { get; set; } = string.Empty;

    public bool Morning { get; set; }

    public bool Afternoon { get; set; }

    public bool Night { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Instructions { get; set; } = string.Empty;

    public MedicineFoodTiming FoodTiming { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
