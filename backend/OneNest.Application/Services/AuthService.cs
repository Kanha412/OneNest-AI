using Microsoft.AspNetCore.Identity;
using OneNest.Application.DTOs.Auth;
using OneNest.Application.Exceptions;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;

namespace OneNest.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
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

        return BuildAuthResponse(user);
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
