using OneNest.Application.DTOs.Admin;
using OneNest.Application.DTOs.Contact;

namespace OneNest.Application.Interfaces.Services;

public interface IAdminService
{
    Task<List<ContactMessageResponse>> GetAllContactMessagesAsync();
    Task<ContactMessageResponse?> UpdateContactStatusAsync(Guid id, UpdateContactStatusRequest request);
    Task<List<AdminUserResponse>> GetAllUsersAsync();
    Task<AdminUserResponse?> UpdateUserRoleAsync(Guid userId, UpdateUserRoleRequest request);
}
