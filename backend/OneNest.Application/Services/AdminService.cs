using OneNest.Application.DTOs.Admin;
using OneNest.Application.DTOs.Contact;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;

namespace OneNest.Application.Services;

public class AdminService : IAdminService
{
    private readonly IContactRepository _contactRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public AdminService(
        IContactRepository contactRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _contactRepository = contactRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<List<ContactMessageResponse>> GetAllContactMessagesAsync()
    {
        var messages = await _contactRepository.GetAllAsync();
        return messages.Select(MapToContactResponse).ToList();
    }

    public async Task<ContactMessageResponse?> UpdateContactStatusAsync(Guid id, UpdateContactStatusRequest request)
    {
        var message = await _contactRepository.GetByIdAsync(id);
        if (message is null) return null;

        message.Status = request.Status;
        message.AdminReply = request.AdminReply;
        message.UpdatedAt = DateTime.UtcNow;

        await _contactRepository.UpdateAsync(message);

        return MapToContactResponse(message);
    }

    public async Task<List<AdminUserResponse>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(u => new AdminUserResponse
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            Role = u.Role,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt
        }).ToList();
    }

    public async Task<AdminUserResponse?> UpdateUserRoleAsync(Guid userId, UpdateUserRoleRequest request)
    {
        // Prevent admin from demoting themselves
        if (userId == _currentUserService.UserId) return null;

        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null) return null;

        var role = request.Role.Trim();
        if (role != "User" && role != "Admin") return null;

        user.Role = role;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        return new AdminUserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    private static ContactMessageResponse MapToContactResponse(Domain.Entities.ContactMessage m) => new()
    {
        Id = m.Id,
        UserId = m.UserId,
        UserName = m.UserName,
        UserEmail = m.UserEmail,
        Subject = m.Subject,
        Message = m.Message,
        Category = m.Category,
        Status = m.Status,
        AdminReply = m.AdminReply,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
