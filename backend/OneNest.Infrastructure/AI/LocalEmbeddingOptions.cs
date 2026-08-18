namespace OneNest.Infrastructure.AI;

/// <summary>
/// Configuration for the local ONNX embedding provider.
/// Bind via <c>"LocalEmbedding"</c> in appsettings.json.
/// </summary>
public class LocalEmbeddingOptions
{
    /// <summary>
    /// Directory where the ONNX model file and vocabulary are stored.
    /// Defaults to <c>~/.onenest/models/all-MiniLM-L6-v2</c> when empty.
    /// Files are downloaded automatically on first use if not present.
    /// </summary>
    public string ModelDirectory { get; set; } = string.Empty;
}
