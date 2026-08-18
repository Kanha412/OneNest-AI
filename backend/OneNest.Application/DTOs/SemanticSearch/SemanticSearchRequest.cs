using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.SemanticSearch;

public class SemanticSearchRequest
{
    /// <summary>Natural-language query to embed and compare against the index.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Maximum results to return (clamped 1–20, default 5).</summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Optional filter — when set, only results of this source type are returned.
    /// Omit to search across all source types.
    /// </summary>
    public EmbeddingSourceType? SourceType { get; set; }
}
