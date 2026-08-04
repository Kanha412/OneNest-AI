namespace OneNest.Application.DTOs.AI;

public class ConversationResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public bool IsArchived { get; set; }
    public List<ChatMessageResponse> Messages { get; set; } = new();
}
