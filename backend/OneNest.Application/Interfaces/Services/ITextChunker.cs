namespace OneNest.Application.Interfaces.Services;

/// <summary>
/// Splits raw document text into overlapping chunks suitable for embedding.
///
/// <list type="bullet">
///   <item>Output is deterministic for the same input and configuration.</item>
///   <item>Chunks are ordered (index 0 = start of document).</item>
///   <item>Every non-empty character in the source text is covered by at
///         least one chunk — no content is silently discarded.</item>
///   <item>Empty or whitespace-only input returns an empty list.</item>
/// </list>
/// </summary>
public interface ITextChunker
{
    /// <summary>
    /// Returns an ordered, non-empty list of text chunks for
    /// <paramref name="text"/>.  Returns an empty list when the input
    /// is null or whitespace.
    /// </summary>
    IReadOnlyList<string> Chunk(string text);
}
