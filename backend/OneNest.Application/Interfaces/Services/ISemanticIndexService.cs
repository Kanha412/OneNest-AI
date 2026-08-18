using OneNest.Domain.Enums;

namespace OneNest.Application.Interfaces.Services;

/// <summary>
/// Builds and maintains the semantic index for a user's workspace items.
/// Called after every create/update/delete in NoteService and DocumentService.
/// </summary>
public interface ISemanticIndexService
{
    /// <summary>
    /// Embeds <paramref name="text"/> and upserts the vector into the index.
    /// Silently returns without throwing if embedding is unavailable.
    /// </summary>
    Task IndexAsync(
        Guid userId,
        EmbeddingSourceType sourceType,
        Guid sourceId,
        string title,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the index entry for a single deleted workspace item.</summary>
    Task DeleteIndexAsync(
        Guid userId,
        EmbeddingSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every embedding record of the given source type for the user.
    /// Used when clearing all documents or all notes in bulk (e.g. "clear storage").
    /// </summary>
    Task DeleteAllBySourceTypeAsync(
        Guid userId,
        EmbeddingSourceType sourceType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes ALL embedding records owned by the user.
    /// Must be called before the user record is deleted (account deletion).
    /// </summary>
    Task DeleteAllByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
