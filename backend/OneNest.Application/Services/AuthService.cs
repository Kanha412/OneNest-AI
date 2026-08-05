using Microsoft.AspNetCore.Identity;
using OneNest.Application.DTOs.Auth;
using OneNest.Application.Exceptions;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Application.Interfaces.Storage;
using OneNest.Domain.Entities;

namespace OneNest.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _userRepository.EmailExistsAsync(email))
            throw new AuthException("An account with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _userRepository.AddAsync(user);

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _userRepository.GetByEmailAsync(email);

        if (user is null)
            throw new AuthException("Invalid email or password.");

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
            throw new AuthException("Invalid email or password.");

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = user.LastLoginAt;
        await _userRepository.UpdateAsync(user);

        return BuildAuthResponse(user);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId)
            ?? throw new AuthException("User not found.");

        var currentPasswordVerification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (currentPasswordVerification == PasswordVerificationResult.Failed)
        {
            throw new AuthException("Current password is incorrect.");
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            throw new AuthException("New password must be different from current password.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);
    }

    public async Task DeleteAccountAsync(DeleteAccountRequest request)
    {
        var userId = _currentUserService.UserId;
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new AuthException("User not found.");

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            throw new AuthException("Password is incorrect.");
        }

        await _userRepository.HardDeleteAccountAsync(userId);
        await _fileStorageService.DeleteUserDirectoryAsync(userId);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Token = token,
            ExpiresAt = expiresAt
        };
    }
}
