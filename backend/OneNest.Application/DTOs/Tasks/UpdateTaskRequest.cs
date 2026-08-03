using OneNest.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace OneNest.Application.DTOs.Tasks;

public class UpdateTaskRequest
{
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public DateOnly? DueDate { get; set; }

    public TaskPriority Priority { get; set; }
}