using OneNest.Application.DTOs.Auth;

namespace OneNest.Application.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);

    Task<AuthResponse> LoginAsync(LoginRequest request);

    Task ChangePasswordAsync(ChangePasswordRequest request);

    Task DeleteAccountAsync(DeleteAccountRequest request);
}
