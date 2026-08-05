using System.ComponentModel.DataAnnotations;

namespace OneNest.Application.DTOs.Auth;

public class DeleteAccountRequest
{
    [Required]
    public string Password { get; set; } = string.Empty;
}
