using System.ComponentModel.DataAnnotations;

namespace OneNest.Application.DTOs.AI;

public class SendMessageRequest
{
    [Required]
    [MaxLength(8000)]
    public string Message { get; set; } = string.Empty;
}
