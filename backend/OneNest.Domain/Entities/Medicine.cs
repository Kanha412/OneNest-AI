using OneNest.Domain.Enums;

namespace OneNest.Domain.Entities;

public class Medicine
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Dosage { get; set; } = string.Empty;

    public string Frequency { get; set; } = string.Empty;

    public bool Morning { get; set; }

    public bool Afternoon { get; set; }

    public bool Night { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Instructions { get; set; } = string.Empty;

    public MedicineFoodTiming FoodTiming { get; set; } = MedicineFoodTiming.Anytime;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
