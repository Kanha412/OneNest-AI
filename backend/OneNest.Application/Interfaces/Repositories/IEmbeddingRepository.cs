using OneNest.Domain.Entities;
using OneNest.Domain.Enums;

namespace OneNest.Application.Interfaces.Repositories;

/// <summary>Carrier returned by similarity search — record plus its cosine similarity score.</summary>
/// <param name="Record">The matched <see cref="EmbeddingRecord"/>.</param>
/// <param name="Score">Cosine similarity ∈ [−1, 1]; higher = more similar.</param>
public record EmbeddingSearchResult(EmbeddingRecord Record, double Score);

public interface IEmbeddingRepository
{
    /// <summary>
    /// Inserts or replaces the embedding for (userId, sourceType, sourceId).
    /// If a record already exists for that triple it is updated in-place.
    /// </summary>
    Task UpsertAsync(EmbeddingRecord record, CancellationToken cancellationToken = default);

    /// <summary>Removes every embedding record for the given source item.</summary>
    Task DeleteBySourceAsync(
        Guid userId,
        EmbeddingSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every embedding record of the given source type owned by the
    /// user.  Used when clearing all documents (or all notes) in bulk.
    /// </summary>
    Task DeleteAllBySourceTypeAsync(
        Guid userId,
        EmbeddingSourceType sourceType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes ALL embedding records owned by the user.
    /// Called on account deletion — must run before the user row is removed.
    /// </summary>
    Task DeleteAllByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <paramref name="topK"/> most similar records to
    /// <paramref name="queryVector"/> that belong to <paramref name="userId"/>,
    /// ordered by descending cosine similarity.
    /// </summary>
    Task<List<EmbeddingSearchResult>> SearchAsync(
        Guid userId,
        float[] queryVector,
        int topK,
        CancellationToken cancellationToken = default);
}
