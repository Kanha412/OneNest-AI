namespace OneNest.Application.Interfaces.Services;

/// <summary>
/// Extracts plain text from document files (PDF, DOCX, TXT, CSV, RTF).
/// Phase 6 — AI Document Intelligence.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>Returns true if this extractor can handle the given file extension (e.g. ".pdf").</summary>
    bool CanExtract(string extension);

    /// <summary>
    /// Extracts plain text from the stream.
    /// Returns null if extraction fails or yields no meaningful text.
    /// </summary>
    Task<string?> ExtractAsync(Stream stream, string extension, CancellationToken cancellationToken = default);
}
