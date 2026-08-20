using Microsoft.Extensions.Logging;
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
    private readonly IEmbeddingProvider              _embeddingProvider;
    private readonly IEmbeddingRepository            _embeddingRepository;
    private readonly ITextChunker                    _textChunker;
    private readonly ILogger<SemanticIndexService>   _logger;

    public SemanticIndexService(
        IEmbeddingProvider             embeddingProvider,
        IEmbeddingRepository           embeddingRepository,
        ITextChunker                   textChunker,
        ILogger<SemanticIndexService>  logger)
    {
        _embeddingProvider   = embeddingProvider;
        _embeddingRepository = embeddingRepository;
        _textChunker         = textChunker;
        _logger              = logger;
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
        {
            _logger.LogWarning(
                "SemanticIndexService: IndexAsync called with empty text for {SourceType} {SourceId}. Skipping.",
                sourceType, sourceId);
            return;
        }

        _logger.LogInformation(
            "SemanticIndexService: IndexAsync started — {SourceType} {SourceId}, text length = {Len} chars.",
            sourceType, sourceId, text.Length);

        // ── Setup: delete stale chunks and split into new ones ────────────────
        // Any failure here is fatal for this source — abort cleanly.

        IReadOnlyList<string> chunks;
        try
        {
            // Delete all existing chunks before re-indexing so edits that shorten
            // a document never leave stale high-index chunks behind.
            await _embeddingRepository.DeleteBySourceAsync(
                userId, sourceType, sourceId, cancellationToken);

            chunks = _textChunker.Chunk(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SemanticIndexService: failed during setup (delete/chunk) for source {SourceId} ({SourceType}). " +
                "Re-index aborted — workspace operation is unaffected.",
                sourceId, sourceType);
            return;
        }

        _logger.LogInformation(
            "SemanticIndexService: indexing {ChunkCount} chunk(s) for {SourceType} {SourceId}.",
            chunks.Count, sourceType, sourceId);

        // ── Per-chunk embedding + upsert ──────────────────────────────────────
        // Each chunk is tried independently.  A failure on one chunk never
        // prevents the remaining chunks from being saved.

        int saved = 0;

        for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            try
            {
                var vector = await _embeddingProvider.EmbedAsync(
                    chunks[chunkIndex], cancellationToken);

                if (vector is null || vector.Length == 0)
                {
                    _logger.LogWarning(
                        "SemanticIndexService: EmbedAsync returned null/empty for chunk {ChunkIndex}/{Total} " +
                        "of source {SourceId} ({SourceType}). " +
                        "Check LocalEmbeddingProvider logs — provider may have failed to initialise.",
                        chunkIndex, chunks.Count, sourceId, sourceType);
                    continue;
                }

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
                saved++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "SemanticIndexService: failed to embed/upsert chunk {ChunkIndex}/{Total} " +
                    "of source {SourceId} ({SourceType}). Chunk skipped; others continue.",
                    chunkIndex, chunks.Count, sourceId, sourceType);
                // Continue — do NOT rethrow; remaining chunks must still be attempted.
            }
        }

        // Always log the final tally so it is easy to confirm success or spot a total failure.
        if (saved == chunks.Count)
            _logger.LogInformation(
                "SemanticIndexService: saved {Saved}/{Total} embedding chunk(s) for {SourceType} {SourceId}. ✓",
                saved, chunks.Count, sourceType, sourceId);
        else
            _logger.LogWarning(
                "SemanticIndexService: only saved {Saved}/{Total} embedding chunk(s) for {SourceType} {SourceId}. " +
                "Check warnings above for the root cause.",
                saved, chunks.Count, sourceType, sourceId);
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
        catch (Exception ex)
        {
            // Best-effort; deletion of the source item still succeeds.
            // Log so silent embedding leaks are diagnosable.
            _logger.LogWarning(ex,
                "SemanticIndexService: failed to delete embedding chunks for {SourceType} {SourceId}. " +
                "Stale chunks may remain in EmbeddingRecords. " +
                "Run POST /api/semantic-search/backfill to clean up.",
                sourceType, sourceId);
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
        catch (Exception ex)
        {
            // Best-effort; bulk workspace deletion still succeeds.
            _logger.LogWarning(ex,
                "SemanticIndexService: failed to bulk-delete embeddings for user {UserId} ({SourceType}).",
                userId, sourceType);
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
        catch (Exception ex)
        {
            // Best-effort; account deletion still proceeds.
            _logger.LogWarning(ex,
                "SemanticIndexService: failed to delete all embeddings for user {UserId} during account teardown.",
                userId);
        }
    }
}
