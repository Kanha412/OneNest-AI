namespace OneNest.Application.DTOs.Notes;

public class NoteResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsPinned { get; set; }

    public bool IsArchived { get; set; }
}