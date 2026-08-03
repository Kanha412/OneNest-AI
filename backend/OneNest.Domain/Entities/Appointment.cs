using OneNest.Domain.Enums;

namespace OneNest.Domain.Entities;

public class Appointment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string DoctorName { get; set; } = string.Empty;

    public string Hospital { get; set; } = string.Empty;

    public string Specialty { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public TimeOnly Time { get; set; }

    public string Notes { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
