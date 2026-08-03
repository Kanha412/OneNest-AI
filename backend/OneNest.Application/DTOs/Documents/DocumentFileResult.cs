namespace OneNest.Application.DTOs.Documents;

public class DocumentFileResult
{
    public Stream Content { get; set; } = Stream.Null;

    public string ContentType { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;
}
