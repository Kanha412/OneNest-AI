using System.ComponentModel.DataAnnotations;

namespace OneNest.Application.DTOs.Auth;

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "Confirm password must match new password.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
