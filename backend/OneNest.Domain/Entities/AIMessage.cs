using OneNest.Domain.Enums;

namespace OneNest.Domain.Entities;

public class AIMessage
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public MessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? TokenCount { get; set; }

    public bool IsError { get; set; }

    public AIConversation? Conversation { get; set; }
}
