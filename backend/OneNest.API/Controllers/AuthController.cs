using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Auth;
using OneNest.Application.Exceptions;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (AuthException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (AuthException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        try
        {
            await _authService.ChangePasswordAsync(request);
            return Ok(new { message = "Password changed successfully." });
        }
        catch (AuthException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request)
    {
        try
        {
            await _authService.DeleteAccountAsync(request);
            return Ok(new { message = "Account deleted successfully." });
        }
        catch (AuthException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
