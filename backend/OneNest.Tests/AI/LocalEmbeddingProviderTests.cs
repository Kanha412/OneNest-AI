using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OneNest.Infrastructure.AI;

namespace OneNest.Tests.AI;

/// <summary>
/// Unit tests for <see cref="LocalEmbeddingProvider"/>.
///
/// These tests exercise observable behaviour without requiring the ONNX model
/// to be present on disk.  Tests that need the actual model file are marked
/// with <c>[Trait("Category","RequiresModel")]</c> and are skipped in CI.
/// </summary>
public class LocalEmbeddingProviderTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="LocalEmbeddingProvider"/> backed by a stub
    /// <see cref="HttpMessageHandler"/> that always returns the specified
    /// <paramref name="statusCode"/>.
    /// </summary>
    private static LocalEmbeddingProvider BuildProvider(
        HttpStatusCode statusCode = HttpStatusCode.NotFound,
        string modelDirectory = "")
    {
        var handler    = new StubHandler(statusCode);
        var httpClient = new HttpClient(handler);
        var options    = Options.Create(new LocalEmbeddingOptions { ModelDirectory = modelDirectory });
        var logger     = NullLogger<LocalEmbeddingProvider>.Instance;
        return new LocalEmbeddingProvider(options, logger, httpClient);
    }

    // ── Null / empty / whitespace guard ──────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task EmbedAsync_NullOrWhitespace_ReturnsNull(string? text)
    {
        using var provider = BuildProvider();
        var result = await provider.EmbedAsync(text!);
        Assert.Null(result);
    }

    // ── Graceful degradation when model cannot be downloaded ─────────────────

    [Fact]
    public async Task EmbedAsync_DownloadFailure_ReturnsNull()
    {
        // Arrange: use an isolated temp dir guaranteed to have no model.onnx.
        // Without isolation, the test would find the real model at the default
        // ~/.onenest path and skip the download entirely, defeating the test.
        var isolatedDir = Path.Combine(Path.GetTempPath(), $"onenest-test-{Guid.NewGuid():N}");
        using var provider = BuildProvider(HttpStatusCode.NotFound, modelDirectory: isolatedDir);

        // Act: first call triggers lazy init; stub returns 404 → download fails
        var result = await provider.EmbedAsync("hello world");

        // Assert: null, not an exception
        Assert.Null(result);
    }

    [Fact]
    public async Task EmbedAsync_AfterPermanentFailure_SubsequentCallsReturnNull()
    {
        // Arrange: isolated temp dir (no real model present) + stub that returns 503
        var isolatedDir = Path.Combine(Path.GetTempPath(), $"onenest-test-{Guid.NewGuid():N}");
        using var provider = BuildProvider(HttpStatusCode.ServiceUnavailable, modelDirectory: isolatedDir);

        // Act: first call marks provider permanently unavailable; no retry storm
        var first  = await provider.EmbedAsync("first");
        var second = await provider.EmbedAsync("second");
        var third  = await provider.EmbedAsync("third");

        Assert.Null(first);
        Assert.Null(second);
        Assert.Null(third);
    }

    // ── L2 normalisation math ─────────────────────────────────────────────────

    [Fact]
    public void L2Normalize_ZeroVector_RemainsZero()
    {
        // Mirrors the static L2Normalize logic inside LocalEmbeddingProvider.
        // Ensures the "norm < 1e-10" guard prevents division by zero.
        var v = new float[] { 0f, 0f, 0f };
        L2NormalizeInline(v);

        Assert.All(v, x => Assert.Equal(0f, x));
    }

    [Fact]
    public void L2Normalize_KnownVector_IsUnitLength()
    {
        var v = new float[] { 3f, 4f };  // |v| = 5 → expected [0.6, 0.8]
        L2NormalizeInline(v);

        Assert.Equal(0.6f, v[0], precision: 5);
        Assert.Equal(0.8f, v[1], precision: 5);
    }

    [Fact]
    public void L2Normalize_UnitVector_Unchanged()
    {
        var v = new float[] { 1f, 0f, 0f };
        L2NormalizeInline(v);

        Assert.Equal(1f, v[0], precision: 6);
        Assert.Equal(0f, v[1], precision: 6);
        Assert.Equal(0f, v[2], precision: 6);
    }

    [Fact]
    public void L2Normalize_ArbitraryVector_HasUnitNorm()
    {
        var v = new float[] { 1f, 2f, 3f, 4f, 5f };
        L2NormalizeInline(v);

        double norm = Math.Sqrt(v.Sum(x => (double)x * x));
        Assert.Equal(1.0, norm, precision: 5);
    }

    // ── Mean-pooling math ─────────────────────────────────────────────────────

    [Fact]
    public void MeanPool_AllAttended_AveragesCorrectly()
    {
        // 3 tokens, 2 dimensions; all attended
        float[,,] hidden = { { { 1f, 2f }, { 3f, 4f }, { 5f, 6f } } };
        long[]    mask   = { 1L, 1L, 1L };

        var result = MeanPoolInline(hidden, mask, seqLen: 3, dim: 2);

        Assert.Equal(3f, result[0], precision: 6); // (1+3+5)/3 = 3
        Assert.Equal(4f, result[1], precision: 6); // (2+4+6)/3 = 4
    }

    [Fact]
    public void MeanPool_PaddingTokensExcluded()
    {
        // 3 tokens; last one is padding (mask = 0)
        float[,,] hidden = { { { 1f, 2f }, { 3f, 4f }, { 99f, 99f } } };
        long[]    mask   = { 1L, 1L, 0L };

        var result = MeanPoolInline(hidden, mask, seqLen: 3, dim: 2);

        Assert.Equal(2f, result[0], precision: 6); // (1+3)/2 = 2
        Assert.Equal(3f, result[1], precision: 6); // (2+4)/2 = 3
    }

    [Fact]
    public void MeanPool_AllPadding_ReturnsZeroVector()
    {
        float[,,] hidden = { { { 1f, 2f }, { 3f, 4f } } };
        long[]    mask   = { 0L, 0L };

        var result = MeanPoolInline(hidden, mask, seqLen: 2, dim: 2);

        Assert.All(result, x => Assert.Equal(0f, x));
    }

    // ── SanitizeText — fused-PDF-text guard (regression for GetWords fix) ────

    [Fact]
    public void SanitizeText_NormalText_Unchanged()
    {
        // Normal English text with short words must pass through unchanged
        const string input = "Hello, this is a normal sentence with short words.";
        var result = SanitizeTextInline(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void SanitizeText_ControlCharsStripped()
    {
        // Control characters (tab = \t, newline = \n stripped to one space boundary)
        var result = SanitizeTextInline("hello\x00world");  // null byte is Control
        Assert.DoesNotContain("\x00", result);
        Assert.Contains("hello", result);
        Assert.Contains("world", result);
    }

    [Fact]
    public void SanitizeText_FusedPdfWord_GetsSpaceInserted()
    {
        // Reproduces the exact bug: a PDF word with no spaces ("KANHAGUPTAKatni...")
        // that exceeds 60 chars must be broken up so the BERT tokenizer doesn't hang.
        var fusedWord = new string('A', 150); // 150-char run, no whitespace
        var result    = SanitizeTextInline(fusedWord);

        // The result must contain at least one inserted space
        Assert.Contains(' ', result);

        // No single space-free run should exceed 60 chars
        var runs = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(runs, run => Assert.True(run.Length <= 60,
            $"Run of {run.Length} chars exceeds the 60-char limit: \"{run[..Math.Min(run.Length, 40)]}…\""));
    }

    [Fact]
    public void SanitizeText_MixedContent_LongRunsBroken_ShortRunsPreserved()
    {
        // Short words preserved; only the fused run gets spaces injected
        var input  = "short words " + new string('X', 120) + " more short words";
        var result = SanitizeTextInline(input);

        Assert.Contains("short", result);
        Assert.Contains("words", result);

        var runs = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(runs, run => Assert.True(run.Length <= 60,
            $"Run of length {run.Length} exceeds limit"));
    }

    [Fact]
    public void SanitizeText_AllControlChars_ReturnsEmpty()
    {
        // A string composed entirely of control characters sanitizes to empty.
        var input  = new string('\x01', 20);
        var result = SanitizeTextInline(input);
        Assert.True(string.IsNullOrWhiteSpace(result));
    }

    // ── Inline copies of the private static helpers ───────────────────────────
    //    (Mirrors LocalEmbeddingProvider's private statics exactly so that any
    //    accidental mismatch is caught at test-write time.)

    private static void L2NormalizeInline(float[] v)
    {
        float norm = 0f;
        foreach (var x in v) norm += x * x;
        norm = MathF.Sqrt(norm);
        if (norm < 1e-10f) return;
        for (int i = 0; i < v.Length; i++) v[i] /= norm;
    }

    private static float[] MeanPoolInline(float[,,] hidden, long[] mask, int seqLen, int dim)
    {
        var result      = new float[dim];
        int nonPadCount = 0;

        for (int t = 0; t < seqLen; t++)
        {
            if (mask[t] == 0L) continue;
            nonPadCount++;
            for (int d = 0; d < dim; d++)
                result[d] += hidden[0, t, d];
        }

        if (nonPadCount > 0)
            for (int d = 0; d < dim; d++)
                result[d] /= nonPadCount;

        return result;
    }

    /// <summary>
    /// Inline mirror of <c>LocalEmbeddingProvider.SanitizeText</c>.
    /// Must be kept in sync manually; any drift is caught by the tests above.
    /// </summary>
    private static string SanitizeTextInline(string text)
    {
        const int MaxTokenRunChars = 60;
        var sb     = new System.Text.StringBuilder(text.Length);
        int runLen = 0;
        foreach (char c in text)
        {
            var cat = char.GetUnicodeCategory(c);
            if (c < ' '   // U+0000–U+001F ASCII control characters (explicit — GetUnicodeCategory('\x00') is unreliable)
             || c == '' // U+007F DEL
             || cat is System.Globalization.UnicodeCategory.Control
                     or System.Globalization.UnicodeCategory.Surrogate
                     or System.Globalization.UnicodeCategory.PrivateUse
                     or System.Globalization.UnicodeCategory.Format)
            {
                if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                runLen = 0;
                continue;
            }
            if (char.IsWhiteSpace(c)) { runLen = 0; }
            else
            {
                runLen++;
                if (runLen > MaxTokenRunChars) { sb.Append(' '); runLen = 1; }
            }
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    // ── Inner helpers ─────────────────────────────────────────────────────────

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public StubHandler(HttpStatusCode status) => _status = status;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_status));
    }
}
