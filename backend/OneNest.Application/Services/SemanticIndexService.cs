using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;

namespace OneNest.Application.Services;

/// <summary>
/// Embeds workspace items and persists their chunk vectors in the semantic index.
///
/// <b>Indexing strategy:</b>
/// Each call to <see cref="IndexAsync"/> first deletes all existing chunks for
/// the source item, then re-chunks the full text and embeds every chunk.  This
/// guarantees stale chunks from previous versions of the document are cleaned up
/// (e.g. when a note is shortened after an edit).
///
/// All operations are best-effort: any failure is silently swallowed so that
/// note/document CRUD operations never fail because of indexing errors.
/// </summary>
public class SemanticIndexService : ISemanticIndexService
{
    private readonly IEmbeddingProvider    _embeddingProvider;
    private readonly IEmbeddingRepository  _embeddingRepository;
    private readonly ITextChunker          _textChunker;

    public SemanticIndexService(
        IEmbeddingProvider   embeddingProvider,
        IEmbeddingRepository embeddingRepository,
        ITextChunker         textChunker)
    {
        _embeddingProvider   = embeddingProvider;
        _embeddingRepository = embeddingRepository;
        _textChunker         = textChunker;
    }

    public async Task IndexAsync(
        Guid                userId,
        EmbeddingSourceType sourceType,
        Guid                sourceId,
        string              title,
        string              text,
        CancellationToken   cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            // Delete all existing chunks for this source item before re-indexing.
            // Handles edits that shorten the document (stale high-index chunks
            // are removed) and ensures ChunkIndex sequence is always clean.
            await _embeddingRepository.DeleteBySourceAsync(
                userId, sourceType, sourceId, cancellationToken);

            var chunks = _textChunker.Chunk(text);

            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                var vector = await _embeddingProvider.EmbedAsync(
                    chunks[chunkIndex], cancellationToken);

                if (vector is null || vector.Length == 0)
                    continue; // best-effort: skip this chunk if embedding failed

                var record = new EmbeddingRecord
                {
                    Id         = Guid.NewGuid(),
                    UserId     = userId,
                    SourceType = sourceType,
                    SourceId   = sourceId,
                    Title      = title,
                    ChunkIndex = chunkIndex,
                    Embedding  = vector,
                    CreatedAt  = DateTime.UtcNow
                };

                await _embeddingRepository.UpsertAsync(record, cancellationToken);
            }
        }
        catch
        {
            // Indexing is best-effort; workspace operations must never fail here
        }
    }

    public async Task DeleteIndexAsync(
        Guid                userId,
        EmbeddingSourceType sourceType,
        Guid                sourceId,
        CancellationToken   cancellationToken = default)
    {
        try
        {
            await _embeddingRepository.DeleteBySourceAsync(
                userId, sourceType, sourceId, cancellationToken);
        }
        catch
        {
            // Best-effort; deletion of the source item still succeeds
        }
    }

    public async Task DeleteAllBySourceTypeAsync(
        Guid                userId,
        EmbeddingSourceType sourceType,
        CancellationToken   cancellationToken = default)
    {
        try
        {
            await _embeddingRepository.DeleteAllBySourceTypeAsync(
                userId, sourceType, cancellationToken);
        }
        catch
        {
            // Best-effort; bulk workspace deletion still succeeds
        }
    }

    public async Task DeleteAllByUserAsync(
        Guid              userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _embeddingRepository.DeleteAllByUserAsync(userId, cancellationToken);
        }
        catch
        {
            // Best-effort; account deletion still proceeds
        }
    }
}
