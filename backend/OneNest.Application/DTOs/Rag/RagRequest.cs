using OneNest.Application.DTOs.AI;

namespace OneNest.Application.DTOs.Rag;

/// <summary>
/// Request payload for the RAG (Retrieval-Augmented Generation) endpoint.
///
/// The <c>UserId</c> is never accepted from the client; it is always resolved
/// server-side from the authenticated JWT via <c>ICurrentUserService</c>.
/// </summary>
public class RagRequest
{
    /// <summary>The natural-language question to answer from personal content.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of distinct source items (notes/documents) to retrieve.
    /// Clamped to [1, 20] server-side.  Null = use server default (5).
    /// </summary>
    public int? TopK { get; set; }

    /// <summary>
    /// Minimum cosine similarity score [0, 1] a chunk must meet to be included.
    /// Null = use server default (0.70).
    /// </summary>
    public double? SimilarityThreshold { get; set; }

    /// <summary>
    /// Optional prior conversation turns to include for multi-turn RAG.
    /// The server caps this list to <c>RagOptions.MaxConversationMessages</c>.
    /// The current <see cref="Query"/> is always appended as the final user
    /// message — do not include it here.
    /// </summary>
    public List<ConversationMessage>? ConversationMessages { get; set; }
}
