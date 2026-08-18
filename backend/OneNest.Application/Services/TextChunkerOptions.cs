namespace OneNest.Application.Services;

/// <summary>
/// Configuration for <see cref="TextChunker"/>.
/// Defaults are calibrated for <b>all-MiniLM-L6-v2</b>, which has a 512
/// WordPiece-token context window.
///
/// Rule of thumb for English text: 1 BERT token ≈ 4 characters.
/// </summary>
public class TextChunkerOptions
{
    /// <summary>
    /// Maximum characters per chunk.
    /// Default 1 200 chars ≈ 300 BERT tokens — leaves headroom for
    /// [CLS]/[SEP] overhead and worst-case token expansion (code, URLs,
    /// rare words) well below the 512-token hard limit.
    /// </summary>
    public int ChunkSizeChars { get; set; } = 1_200;

    /// <summary>
    /// Characters of overlap between consecutive chunks.
    /// Default 240 chars ≈ 60 BERT tokens — provides context continuity
    /// across chunk boundaries without doubling storage or inference cost.
    /// Must be strictly less than <see cref="ChunkSizeChars"/>.
    /// </summary>
    public int ChunkOverlapChars { get; set; } = 240;
}
