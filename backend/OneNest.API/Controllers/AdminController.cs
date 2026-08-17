using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Admin;
using OneNest.Application.DTOs.Contact;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    // ── Messages ──────────────────────────────────────────────

    [HttpGet("messages")]
    public async Task<ActionResult<List<ContactMessageResponse>>> GetAllMessages()
    {
        var messages = await _adminService.GetAllContactMessagesAsync();
        return Ok(messages);
    }

    [HttpPatch("messages/{id:guid}/status")]
    public async Task<ActionResult<ContactMessageResponse>> UpdateMessageStatus(
        Guid id,
        UpdateContactStatusRequest request)
    {
        var message = await _adminService.UpdateContactStatusAsync(id, request);
        if (message is null) return NotFound();
        return Ok(message);
    }

    // ── Users ─────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserResponse>>> GetAllUsers()
    {
        var users = await _adminService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpPatch("users/{id:guid}/role")]
    public async Task<ActionResult<AdminUserResponse>> UpdateUserRole(
        Guid id,
        UpdateUserRoleRequest request)
    {
        var user = await _adminService.UpdateUserRoleAsync(id, request);
        if (user is null) return BadRequest(new { message = "Cannot update this user's role." });
        return Ok(user);
    }
}
