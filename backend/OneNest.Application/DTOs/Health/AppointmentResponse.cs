using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Health;

public class AppointmentResponse
{
    public Guid Id { get; set; }

    public string DoctorName { get; set; } = string.Empty;

    public string Hospital { get; set; } = string.Empty;

    public string Specialty { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public TimeOnly Time { get; set; }

    public string Notes { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
