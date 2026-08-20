using OneNest.Application.DTOs.Contact;

namespace OneNest.Application.Interfaces.Services;

public interface IContactService
{
    Task<ContactMessageResponse> CreateAsync(CreateContactRequest request);
    Task<List<ContactMessageResponse>> GetMyMessagesAsync();
    Task<ContactSummaryResponse> GetSummaryAsync();
}
