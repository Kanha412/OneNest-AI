using OneNest.Domain.Enums;

namespace OneNest.Domain.Entities;

public class ContactMessage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ContactCategory Category { get; set; } = ContactCategory.General;
    public ContactStatus Status { get; set; } = ContactStatus.New;
    public string? AdminReply { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
