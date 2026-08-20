using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OneNest.Application.Interfaces.Services;

namespace OneNest.Application.Services;

/// <summary>
/// Sentence-aware, overlapping text chunker for semantic embedding.
///
/// <b>Algorithm</b>
/// <list type="number">
///   <item>Normalize whitespace: collapse runs of spaces/tabs, reduce runs of
///         3+ newlines to double-newlines, strip leading/trailing space.</item>
///   <item>Segment: split the normalized text at sentence boundaries
///         (after . ! ? followed by whitespace) and at paragraph breaks
///         (two or more newlines). The sentence-ending punctuation stays
///         attached to its segment.</item>
///   <item>Greedily assemble segments into chunks up to
///         <see cref="TextChunkerOptions.ChunkSizeChars"/>.</item>
///   <item>If a single segment exceeds the limit, hard-split at word
///         boundaries so no content is lost.</item>
///   <item>Compute overlap: step back from the end of each finished chunk by
///         <see cref="TextChunkerOptions.ChunkOverlapChars"/> characters,
///         aligned to segment boundaries. The next chunk begins there.</item>
///   <item>Always advance at least one segment so the loop terminates.</item>
/// </list>
///
/// This implementation is deterministic (given constant options), preserves
/// chunk order, normalizes whitespace, and never discards text.
/// </summary>
public sealed class TextChunker : ITextChunker
{
    // Split AFTER sentence-ending punctuation followed by whitespace,
    // OR at any line break (single or multiple newlines).
    //
    // WHY single newlines: PDF-extracted text uses \n between every logical
    // line (bullet points, section headers, contact info, job titles).
    // The original \n{2+} pattern treated these as one giant segment, causing
    // HardSplitByWords to receive the entire document as a single piece.
    // Single-\n splitting correctly breaks a resume into sentence/line-size
    // segments so the chunker can pack them into properly-sized chunks.
    private static readonly Regex SegmentSplitter = new(
        @"(?<=[.!?])\s+|\n+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MultiNewline = new(
        @"\n{3,}", RegexOptions.Compiled);

    private static readonly Regex HorizontalWs = new(
        @"[ \t]+", RegexOptions.Compiled);

    private readonly TextChunkerOptions _options;

    public TextChunker(IOptions<TextChunkerOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var normalized = NormalizeWhitespace(text);
        var segments   = Segment(normalized);

        if (segments.Count == 0) return [];

        // Fast path: document fits in a single chunk
        if (segments.Count == 1 && segments[0].Length <= _options.ChunkSizeChars)
            return [segments[0]];

        var result = new List<string>();
        int i = 0;

        while (i < segments.Count)
        {
            var sb    = new StringBuilder();
            int j     = i;

            // Greedily accumulate segments until adding the next would overflow
            while (j < segments.Count)
            {
                var sep       = sb.Length > 0 ? " " : string.Empty;
                var candidate = segments[j];

                if (sb.Length + sep.Length + candidate.Length > _options.ChunkSizeChars)
                    break;

                sb.Append(sep);
                sb.Append(candidate);
                j++;
            }

            if (j == i)
            {
                // A single segment is too long — hard-split at word boundaries
                // to guarantee no content is dropped.
                foreach (var sub in HardSplitByWords(segments[i], _options.ChunkSizeChars))
                    result.Add(sub);
                i++;
                continue;
            }

            var chunk = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
                result.Add(chunk);

            // All segments consumed — done.
            if (j >= segments.Count) break;

            // Compute overlap: step back from j until we have covered
            // at least ChunkOverlapChars, staying on segment boundaries.
            int overlapChars = 0;
            int nextStart    = j;

            while (nextStart > i && overlapChars < _options.ChunkOverlapChars)
            {
                nextStart--;
                overlapChars += segments[nextStart].Length + 1; // +1 for separator space
            }

            // Always advance at least one segment beyond i to guarantee termination.
            i = nextStart > i ? nextStart : i + 1;
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizeWhitespace(string text)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                   .Replace('\r', '\n');
        text = MultiNewline.Replace(text, "\n\n");
        text = HorizontalWs.Replace(text, " ");
        return text.Trim();
    }

    private static List<string> Segment(string text)
    {
        return SegmentSplitter
            .Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Splits a single over-long string at word boundaries.
    /// Each yielded fragment is ≤ <paramref name="maxChars"/> characters.
    ///
    /// Splits on <em>any whitespace</em> (space, tab, newline) so that
    /// PDF-extracted text — which uses \n as a word separator — is chunked
    /// correctly instead of being returned as a single oversized string.
    /// </summary>
    private static IEnumerable<string> HardSplitByWords(string text, int maxChars)
    {
        // null separator → split on all whitespace characters (space, \t, \n, \r, …)
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var sb    = new StringBuilder();

        foreach (var word in words)
        {
            if (sb.Length == 0)
            {
                // Word itself exceeds limit — yield it as-is to avoid losing content
                if (word.Length > maxChars)
                {
                    if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
                    yield return word;
                    continue;
                }
                sb.Append(word);
                continue;
            }

            if (sb.Length + 1 + word.Length > maxChars)
            {
                yield return sb.ToString();
                sb.Clear();
                sb.Append(word);
            }
            else
            {
                sb.Append(' ');
                sb.Append(word);
            }
        }

        if (sb.Length > 0) yield return sb.ToString();
    }
}
