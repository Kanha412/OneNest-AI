using OneNest.Application.DTOs.Contact;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;

namespace OneNest.Application.Services;

public class ContactService : IContactService
{
    private readonly IContactRepository _contactRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public ContactService(
        IContactRepository contactRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _contactRepository = contactRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ContactMessageResponse> CreateAsync(CreateContactRequest request)
    {
        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId);

        var message = new ContactMessage
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId,
            UserName = user?.FullName ?? string.Empty,
            UserEmail = user?.Email ?? string.Empty,
            Subject = request.Subject,
            Message = request.Message,
            Category = request.Category,
            CreatedAt = DateTime.UtcNow
        };

        await _contactRepository.AddAsync(message);

        return MapToResponse(message);
    }

    public async Task<List<ContactMessageResponse>> GetMyMessagesAsync()
    {
        var messages = await _contactRepository.GetByUserIdAsync(_currentUserService.UserId);
        return messages.Select(MapToResponse).ToList();
    }

    private static ContactMessageResponse MapToResponse(ContactMessage m) => new()
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
