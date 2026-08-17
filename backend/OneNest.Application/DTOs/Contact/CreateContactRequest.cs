using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Contact;

public class CreateContactRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ContactCategory Category { get; set; } = ContactCategory.General;
}
