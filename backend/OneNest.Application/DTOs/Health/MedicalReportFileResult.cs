namespace OneNest.Application.DTOs.Health;

public class MedicalReportFileResult
{
    public Stream Content { get; set; } = Stream.Null;

    public string ContentType { get; set; } = "application/octet-stream";

    public string OriginalFileName { get; set; } = string.Empty;
}
