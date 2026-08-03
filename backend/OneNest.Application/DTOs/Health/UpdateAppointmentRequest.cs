using System.ComponentModel.DataAnnotations;
using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Health;

public class UpdateAppointmentRequest
{
    [Required]
    [MaxLength(150)]
    public string DoctorName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string Hospital { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Specialty { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public TimeOnly Time { get; set; }

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
}
