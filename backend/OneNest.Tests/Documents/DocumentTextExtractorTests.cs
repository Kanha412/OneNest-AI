using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OneNest.Infrastructure.Documents;
using Xunit;

namespace OneNest.Tests.Documents;

public class DocumentTextExtractorTests
{
    private readonly DocumentTextExtractor _extractor = new(NullLogger<DocumentTextExtractor>.Instance);

    // ── CanExtract ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(".pdf",  true)]
    [InlineData(".PDF",  true)]   // case-insensitive
    [InlineData(".docx", true)]
    [InlineData(".DOCX", true)]
    [InlineData(".txt",  true)]
    [InlineData(".csv",  true)]
    [InlineData(".rtf",  true)]
    [InlineData(".doc",  false)]  // unsupported old-format Word
    [InlineData(".xls",  false)]
    [InlineData(".xlsx", false)]
    [InlineData(".png",  false)]
    [InlineData(".jpg",  false)]
    [InlineData("",      false)]
    public void CanExtract_ReturnsExpected(string extension, bool expected)
    {
        Assert.Equal(expected, _extractor.CanExtract(extension));
    }

    // ── TXT extraction ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_Txt_ReturnsContent()
    {
        const string content = "Hello Phase 6!\nThis is a test document.";
        using var stream = ToStream(content);

        var result = await _extractor.ExtractAsync(stream, ".txt");

        Assert.NotNull(result);
        Assert.Contains("Hello Phase 6!", result);
    }

    [Fact]
    public async Task ExtractAsync_Csv_ReturnsContent()
    {
        const string content = "Name,Age\nAlice,30\nBob,25";
        using var stream = ToStream(content);

        var result = await _extractor.ExtractAsync(stream, ".csv");

        Assert.NotNull(result);
        Assert.Contains("Alice", result);
    }

    [Fact]
    public async Task ExtractAsync_Rtf_ReturnsContent()
    {
        // RTF files start with {\rtf — treat as plain text for our StreamReader path
        const string content = @"{\rtf1\ansi Sample RTF content for OneNest.}";
        using var stream = ToStream(content);

        var result = await _extractor.ExtractAsync(stream, ".rtf");

        Assert.NotNull(result);
        Assert.Contains("Sample RTF content", result);
    }

    [Fact]
    public async Task ExtractAsync_EmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();

        var result = await _extractor.ExtractAsync(stream, ".txt");

        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractAsync_WhitespaceOnlyContent_ReturnsNull()
    {
        using var stream = ToStream("   \n\n\t  ");

        var result = await _extractor.ExtractAsync(stream, ".txt");

        Assert.Null(result);
    }

    // ── Unsupported extension ───────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_UnsupportedExtension_ReturnsNull()
    {
        using var stream = ToStream("binary content");

        var result = await _extractor.ExtractAsync(stream, ".exe");

        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractAsync_NullStream_ReturnsNull()
    {
        var result = await _extractor.ExtractAsync(null!, ".txt");

        Assert.Null(result);
    }

    // ── 50 000-char cap ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_Txt_CapAt50000Chars()
    {
        // Build a string well over the 50k cap
        var bigContent = new string('A', 55_000);
        using var stream = ToStream(bigContent);

        var result = await _extractor.ExtractAsync(stream, ".txt");

        Assert.NotNull(result);
        Assert.True(result!.Length <= 50_000, $"Expected ≤50000 chars, got {result.Length}");
    }

    // ── Malformed DOCX gracefully returns null ──────────────────────────────

    [Fact]
    public async Task ExtractAsync_MalformedDocx_ReturnsNull()
    {
        // Feed random bytes as a DOCX — OpenXml should throw internally,
        // extractor must swallow it and return null.
        var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF };
        using var stream = new MemoryStream(garbage);

        var result = await _extractor.ExtractAsync(stream, ".docx");

        Assert.Null(result);
    }

    // ── Malformed PDF gracefully returns null ───────────────────────────────

    [Fact]
    public async Task ExtractAsync_MalformedPdf_ReturnsNull()
    {
        var garbage = new byte[] { 0x25, 0x50, 0x44, 0x46, 0xFF }; // starts with %PDF then garbage
        using var stream = new MemoryStream(garbage);

        var result = await _extractor.ExtractAsync(stream, ".pdf");

        Assert.Null(result);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static MemoryStream ToStream(string text) =>
        new(Encoding.UTF8.GetBytes(text));
}
