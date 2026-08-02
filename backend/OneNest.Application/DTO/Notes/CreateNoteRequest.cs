namespace OneNest.Application.DTOs.Notes;

public class CreateNoteRequest
{
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}