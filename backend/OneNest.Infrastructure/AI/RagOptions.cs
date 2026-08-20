namespace OneNest.Infrastructure.AI;

/// <summary>
/// Configuration options for the RAG (Retrieval-Augmented Generation) pipeline.
/// Bound from the <c>RAG</c> section in <c>appsettings.json</c>.
/// </summary>
public class RagOptions
{
    /// <summary>
    /// Maximum number of distinct source items (notes/documents) to retrieve
    /// and use as context.  Clamped to [1, 20] at runtime.  Default: 5.
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Minimum cosine similarity score [0, 1] a chunk must meet to be included
    /// in the context.  Chunks below this threshold are silently dropped.
    /// Default: 0.70.
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.70;

    /// <summary>
    /// Hard cap on the total characters contributed by all source texts in the
    /// context block.  Prevents the Gemini prompt from exceeding the context
    /// window.  Default: 12 000.
    /// </summary>
    public int MaxContextCharacters { get; set; } = 12_000;

    /// <summary>
    /// Maximum number of prior conversation messages (excluding the current
    /// query) to include for multi-turn RAG.  Default: 10.
    /// </summary>
    public int MaxConversationMessages { get; set; } = 10;
}
