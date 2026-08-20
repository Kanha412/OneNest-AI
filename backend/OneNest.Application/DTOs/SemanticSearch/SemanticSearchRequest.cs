using System.Text.Json.Serialization;
using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.SemanticSearch;

public class SemanticSearchRequest
{
    /// <summary>Natural-language query to embed and compare against the index.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Maximum results to return (clamped 1–20, default 5).</summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Optional source-type filter.
    /// Omit this field (or send <c>null</c>) to search across all types.
    /// Sending <c>0</c> filters to <b>Notes</b> only; <c>1</c> filters to <b>Documents</b> only.
    ///
    /// <b>Important (Swagger users):</b> Swagger pre-fills this field with <c>0</c> (= Note).
    /// Delete the field from the request body or set it to <c>null</c> if you want
    /// results from both notes and documents.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EmbeddingSourceType? SourceType { get; set; }
}
