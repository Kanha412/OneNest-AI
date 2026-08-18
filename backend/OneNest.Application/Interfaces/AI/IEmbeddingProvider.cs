namespace OneNest.Application.Interfaces.AI;

/// <summary>
/// Abstraction over a text-embedding model.
/// The concrete implementation lives in Infrastructure; swapping providers
/// (Gemini, Local ONNX, etc.) requires only a configuration change — no
/// changes to Application or Domain.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Returns an L2-normalised embedding vector for <paramref name="text"/>,
    /// or <c>null</c> if embedding is unavailable (model not loaded,
    /// API key missing, inference error, empty input, etc.).
    ///
    /// The dimension of the returned array is determined by the active
    /// provider and the <c>Embeddings:Dimension</c> configuration value.
    /// It must match the <c>vector(N)</c> column in the database.
    /// </summary>
    Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
