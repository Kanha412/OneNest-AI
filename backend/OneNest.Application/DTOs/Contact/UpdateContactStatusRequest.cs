using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Contact;

public class UpdateContactStatusRequest
{
    public ContactStatus Status { get; set; }
    public string? AdminReply { get; set; }
}
