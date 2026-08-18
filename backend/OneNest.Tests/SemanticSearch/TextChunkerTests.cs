using Microsoft.Extensions.Options;
using OneNest.Application.Services;

namespace OneNest.Tests.SemanticSearch;

/// <summary>
/// Unit tests for <see cref="TextChunker"/>.
/// All tests run without ONNX or any infrastructure dependency.
/// </summary>
public class TextChunkerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TextChunker MakeChunker(int chunkSize = 1_200, int overlap = 240)
    {
        var opts = Options.Create(new TextChunkerOptions
        {
            ChunkSizeChars    = chunkSize,
            ChunkOverlapChars = overlap
        });
        return new TextChunker(opts);
    }

    private static string Repeat(string sentence, int times) =>
        string.Join(" ", Enumerable.Repeat(sentence, times));

    // ── Null / empty / whitespace ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Chunk_NullOrWhitespace_ReturnsEmpty(string? text)
    {
        var chunks = MakeChunker().Chunk(text!);
        Assert.Empty(chunks);
    }

    // ── Short document fits in one chunk ──────────────────────────────────────

    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunk()
    {
        const string text = "Hello world. How are you?";
        var chunks = MakeChunker().Chunk(text);

        Assert.Single(chunks);
        Assert.Contains("Hello world", chunks[0]);
        Assert.Contains("How are you", chunks[0]);
    }

    // ── Text exactly at boundary → one chunk ─────────────────────────────────

    [Fact]
    public void Chunk_TextExactlyAtLimit_ReturnsSingleChunk()
    {
        // 100 'a' chars — exactly at the limit
        var text   = new string('a', 100);
        var chunks = MakeChunker(chunkSize: 100, overlap: 20).Chunk(text);

        Assert.Single(chunks);
    }

    // ── Long document produces multiple chunks ────────────────────────────────

    [Fact]
    public void Chunk_LongDocument_ReturnsMultipleChunks()
    {
        // 10 sentences × 200 chars each = 2000 chars >> chunkSize of 500
        var text   = Repeat("This is a long sentence that is about two hundred characters when counted fully. ", 12);
        var chunks = MakeChunker(chunkSize: 500, overlap: 100).Chunk(text);

        Assert.True(chunks.Count > 1, $"Expected multiple chunks, got {chunks.Count}");
    }

    // ── No content is lost ────────────────────────────────────────────────────

    [Fact]
    public void Chunk_AllTextCovered_NothingDropped()
    {
        // Use a uniquely-identifiable word at the very end of a long document
        var body      = Repeat("Regular sentence content appears here many times. ", 30);
        var lastWord  = "UNIQUE_SENTINEL_WORD";
        var text      = body + " " + lastWord;

        var chunks = MakeChunker(chunkSize: 500, overlap: 100).Chunk(text);

        // The sentinel must appear in at least one chunk
        Assert.Contains(chunks, c => c.Contains(lastWord));
    }

    // ── Chunks are ordered ────────────────────────────────────────────────────

    [Fact]
    public void Chunk_ChunksAreOrdered_FirstChunkContainsStart()
    {
        var text   = "FIRST_WORD. " + Repeat("Middle content here. ", 20) + " LAST_WORD.";
        var chunks = MakeChunker(chunkSize: 300, overlap: 60).Chunk(text);

        Assert.Contains("FIRST_WORD",  chunks[0]);
        Assert.Contains("LAST_WORD",   chunks[^1]);
    }

    // ── Consecutive chunks share overlapping content ──────────────────────────

    [Fact]
    public void Chunk_ConsecutiveChunks_HaveOverlap()
    {
        // 8 sentences of ~60 chars each = ~480 chars total, chunkSize=200, overlap=60
        var sentences = new[]
        {
            "Alpha sentence one is here. ",
            "Beta sentence two is here. ",
            "Gamma sentence three here. ",
            "Delta sentence four here. ",
            "Epsilon sentence five here. ",
            "Zeta sentence six is here. ",
            "Eta sentence seven is here. ",
            "Theta sentence eight here. "
        };
        var text   = string.Concat(sentences);
        var chunks = MakeChunker(chunkSize: 200, overlap: 60).Chunk(text);

        if (chunks.Count < 2) return; // trivially no overlap for very short text

        // Find a word that appears in both chunk[0] and chunk[1]
        var words0 = chunks[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words1 = chunks[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var shared  = words0.Intersect(words1).ToList();

        Assert.True(shared.Count > 0,
            $"Expected some overlap between chunk 0 and chunk 1.\n" +
            $"Chunk 0: {chunks[0]}\n" +
            $"Chunk 1: {chunks[1]}");
    }

    // ── Very long single sentence is hard-split at words ─────────────────────

    [Fact]
    public void Chunk_SingleLongSentence_SplitsAtWordBoundaries()
    {
        // 50 words × ~10 chars each = ~500 chars, chunkSize=200
        var text   = string.Join(" ", Enumerable.Repeat("longwordhere", 50));
        var chunks = MakeChunker(chunkSize: 200, overlap: 40).Chunk(text);

        Assert.True(chunks.Count > 1);
        foreach (var chunk in chunks)
            Assert.True(chunk.Length <= 200,
                $"Chunk length {chunk.Length} exceeds limit: {chunk[..Math.Min(60, chunk.Length)]}…");
    }

    // ── Whitespace normalisation ──────────────────────────────────────────────

    [Fact]
    public void Chunk_ExcessiveWhitespace_IsNormalized()
    {
        const string text = "Hello   world.\r\n\r\nNew    paragraph.";
        var chunks = MakeChunker().Chunk(text);

        foreach (var chunk in chunks)
        {
            Assert.DoesNotMatch(@"  +",  chunk);  // no double spaces
            Assert.DoesNotMatch(@"\r",   chunk);  // no carriage returns
        }
    }

    // ── Deterministic output ──────────────────────────────────────────────────

    [Fact]
    public void Chunk_SameInput_ProducesSameOutput()
    {
        var text    = Repeat("Determinism test sentence. ", 25);
        var chunker = MakeChunker(chunkSize: 400, overlap: 80);

        var run1 = chunker.Chunk(text);
        var run2 = chunker.Chunk(text);

        Assert.Equal(run1.Count, run2.Count);
        for (int i = 0; i < run1.Count; i++)
            Assert.Equal(run1[i], run2[i]);
    }

    // ── Content after 8 000 chars is not lost ─────────────────────────────────

    [Fact]
    public void Chunk_TextLongerThan8000Chars_ContentAfter8000IsPresent()
    {
        // Build ~9 500 char prefix: "Regular filler sentence content is here. " = 41 chars
        // 220 repetitions × (41 + 1 separator) ≈ 9 240 chars → sentinel lands past position 8 000
        var prefix    = Repeat("Regular filler sentence content is here. ", 220);
        var sentinel  = "AFTER_EIGHT_THOUSAND_CHARS_MARKER";
        var suffix    = " More content follows the marker with additional sentences.";
        var text      = prefix + sentinel + suffix;

        Assert.True(text.Length > 8_000, $"Test text is only {text.Length} chars — increase prefix.");

        var chunks = MakeChunker().Chunk(text);

        Assert.True(chunks.Count > 1, "Expected multiple chunks for a 15 K document.");
        Assert.Contains(chunks, c => c.Contains(sentinel));
    }

    // ── Configurable chunk size ───────────────────────────────────────────────

    [Theory]
    [InlineData(300)]
    [InlineData(600)]
    [InlineData(1_200)]
    public void Chunk_NoChunkExceedsConfiguredSize(int chunkSize)
    {
        var text   = Repeat("Sentence to test size limits thoroughly. ", 40);
        var chunks = MakeChunker(chunkSize: chunkSize, overlap: chunkSize / 5).Chunk(text);

        foreach (var chunk in chunks)
            Assert.True(chunk.Length <= chunkSize,
                $"Chunk of length {chunk.Length} exceeds configured limit {chunkSize}.");
    }
}
