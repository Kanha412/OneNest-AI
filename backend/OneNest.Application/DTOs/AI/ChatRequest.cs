using System.ComponentModel.DataAnnotations;

namespace OneNest.Application.DTOs.AI;

public class ChatRequest
{
    [Required]
    [MaxLength(4000)]
    public string Message { get; set; } = string.Empty;
}
