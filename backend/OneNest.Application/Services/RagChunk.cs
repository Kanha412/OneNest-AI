namespace OneNest.Application.Services;

/// <summary>
/// Internal transfer object carrying one retrieved source chunk into
/// <see cref="RagContextBuilder"/>.
///
/// <b>Lifetime:</b> created inside <c>RagService</c>, consumed by
/// <c>RagContextBuilder.Build</c>, then discarded.  Never serialised to JSON.
/// </summary>
/// <param name="SourceType">Human-readable source category: "Note" or "Document".</param>
/// <param name="Title">Title of the source item.</param>
/// <param name="Text">
/// Full text of the note or the extracted text of the document.
/// May be empty when extraction was unavailable.
/// </param>
/// <param name="Score">Cosine similarity ∈ [0, 1].</param>
/// <param name="ChunkIndex">Zero-based chunk index within the source.</param>
public sealed record RagChunk(
    string SourceType,
    string Title,
    string Text,
    double Score,
    int    ChunkIndex);
