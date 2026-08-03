namespace OneNest.Application.DTOs.Health;

public class UploadMedicalReportInput
{
    public CreateMedicalReportRequest Metadata { get; set; } = new();

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public Stream Content { get; set; } = Stream.Null;
}
