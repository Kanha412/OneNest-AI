using System.ComponentModel.DataAnnotations;

namespace OneNest.Application.DTOs.AI;

public class RenameConversationRequest
{
    [Required]
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;
}
