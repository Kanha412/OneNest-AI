using System.ComponentModel.DataAnnotations;
using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Health;

public class UpdateMedicineRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Dosage { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Frequency { get; set; } = string.Empty;

    public bool Morning { get; set; }

    public bool Afternoon { get; set; }

    public bool Night { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [MaxLength(1000)]
    public string Instructions { get; set; } = string.Empty;

    public MedicineFoodTiming FoodTiming { get; set; } = MedicineFoodTiming.Anytime;

    public bool IsActive { get; set; } = true;
}
