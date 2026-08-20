using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using OneNest.Application.Interfaces.Services;
using UglyToad.PdfPig;

namespace OneNest.Infrastructure.Documents;

/// <summary>
/// Extracts plain text from uploaded documents.
/// Supported: PDF (PdfPig), DOCX (OpenXml), TXT/CSV/RTF (StreamReader).
/// Phase 6 — AI Document Intelligence.
/// </summary>
public class DocumentTextExtractor : IDocumentTextExtractor
{
    private const int MaxExtractedChars = 50_000; // safety cap to avoid bloating DB

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt", ".csv", ".rtf"
    };

    private readonly ILogger<DocumentTextExtractor> _logger;

    public DocumentTextExtractor(ILogger<DocumentTextExtractor> logger)
    {
        _logger = logger;
    }

    public bool CanExtract(string extension) =>
        !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension);

    public async Task<string?> ExtractAsync(Stream stream, string extension, CancellationToken cancellationToken = default)
    {
        if (stream is null || !stream.CanRead)
            return null;

        var ext = extension.Trim().ToLowerInvariant();

        try
        {
            return ext switch
            {
                ".pdf"  => await ExtractPdfAsync(stream, cancellationToken),
                ".docx" => await ExtractDocxAsync(stream, cancellationToken),
                ".txt" or ".csv" or ".rtf" => await ExtractTextAsync(stream, cancellationToken),
                _ => null
            };
        }
        catch (Exception ex)
        {
            // Extraction is best-effort — never let it crash the upload flow.
            // Log the real exception so silent failures are diagnosable.
            _logger.LogWarning(ex,
                "DocumentTextExtractor: extraction threw for '{Extension}'. " +
                "Possible causes: image-only PDF, encrypted PDF, unsupported format. " +
                "Semantic indexing will be skipped for this file.",
                ext);
            return null;
        }
    }

    // ── PDF via PdfPig ──────────────────────────────────────────────────────

    private static Task<string?> ExtractPdfAsync(Stream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // PdfPig needs a byte array; stream may not be seekable after upload
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        if (bytes.Length == 0)
            return Task.FromResult<string?>(null);

        using var pdf = PdfDocument.Open(bytes);
        var sb = new StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // CRITICAL: use GetWords() instead of page.Text.
            //
            // page.Text concatenates character glyphs WITHOUT inserting spaces
            // between words for many PDF layouts (e.g. resumes, multi-column
            // forms, and any PDF where word spacing is encoded as glyph offsets
            // rather than explicit space characters).
            // Result: "KANHAGUPTAKatni,MadhyaPradesh+919407..." — one giant
            // space-free string.  The BERT WordPiece tokenizer is O(n²) on
            // long unknown words and hangs for 10+ minutes on such input.
            //
            // GetWords() uses PdfPig's built-in word-segmentation algorithm
            // which analyses glyph positions and bounding boxes to detect word
            // boundaries correctly — the same approach used by PDF viewers.
            // Joining with a single space yields naturally-tokenizable text.
            var pageWords = page.GetWords();
            var pageText  = string.Join(" ", pageWords.Select(w => w.Text));

            if (!string.IsNullOrWhiteSpace(pageText))
            {
                sb.AppendLine(pageText);
            }

            if (sb.Length >= MaxExtractedChars)
                break;
        }

        var text = sb.ToString().Trim();
        if (text.Length > MaxExtractedChars)
            text = text[..MaxExtractedChars];

        return Task.FromResult<string?>(string.IsNullOrWhiteSpace(text) ? null : text);
    }

    // ── DOCX via DocumentFormat.OpenXml ────────────────────────────────────

    private static Task<string?> ExtractDocxAsync(Stream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // OpenXml requires seekable stream — copy to MemoryStream first
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        if (ms.Length == 0)
            return Task.FromResult<string?>(null);

        using var wordDoc = WordprocessingDocument.Open(ms, isEditable: false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;

        if (body is null)
            return Task.FromResult<string?>(null);

        var sb = new StringBuilder();

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lineText = paragraph.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(lineText))
                sb.AppendLine(lineText);

            if (sb.Length >= MaxExtractedChars)
                break;
        }

        var text = sb.ToString().Trim();
        if (text.Length > MaxExtractedChars)
            text = text[..MaxExtractedChars];

        return Task.FromResult<string?>(string.IsNullOrWhiteSpace(text) ? null : text);
    }

    // ── TXT / CSV / RTF via StreamReader ───────────────────────────────────

    private static async Task<string?> ExtractTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        text = text?.Trim() ?? string.Empty;

        if (text.Length > MaxExtractedChars)
            text = text[..MaxExtractedChars];

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
