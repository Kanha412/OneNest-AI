using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.SemanticSearch;

public class SemanticSearchResult
{
    /// <summary>PK of the source entity (Note.Id or Document.Id).</summary>
    public Guid SourceId { get; set; }

    /// <summary>Whether the result comes from a note or a document.</summary>
    public EmbeddingSourceType SourceType { get; set; }

    /// <summary>Human-readable label of the source item.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Cosine similarity score ∈ [0, 1]; higher means more relevant.</summary>
    public double Score { get; set; }
}
