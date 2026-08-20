namespace OneNest.Application.DTOs.Rag;

/// <summary>
/// Metadata about a single source item that grounded the RAG answer.
///
/// <b>Privacy note:</b> the internal <c>SourceId</c> (PK of the Note or Document)
/// is intentionally NOT included here.  Clients receive only enough metadata to
/// display a readable citation — not to directly query the underlying entity.
/// </summary>
public class RagSourceDto
{
    /// <summary>"Note" or "Document" — human-readable source category.</summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>Title of the source note or document.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Zero-based index of the chunk within the source that had the highest
    /// similarity score.  Useful for understanding which part of a long document
    /// was most relevant.
    /// </summary>
    public int ChunkIndex { get; set; }
}
