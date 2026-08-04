namespace OneNest.Domain.Entities;

public class AIConversation
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; }

    public bool IsDeleted { get; set; }

    public List<AIMessage> Messages { get; set; } = new();
}
