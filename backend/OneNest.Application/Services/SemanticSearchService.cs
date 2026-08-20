using Microsoft.Extensions.Logging;
using OneNest.Application.DTOs.SemanticSearch;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Services;

namespace OneNest.Application.Services;

/// <summary>
/// Embeds the search query and returns the most semantically similar
/// workspace items from the user's personal index.
///
/// <b>Deduplication:</b> Because a single document may be indexed as multiple
/// chunks, the raw repository results can contain several rows with the same
/// <c>(SourceType, SourceId)</c>.  This service groups by source and keeps
/// only the highest-scoring chunk per source before returning results —
/// so the caller always receives at most one result per document/note.
/// </summary>
public class SemanticSearchService : ISemanticSearchService
{
    private const int MinTopK = 1;
    private const int MaxTopK = 20;

    // Multiplier applied to topK when fetching from the repository to give the
    // deduplication step enough raw candidates.
    // Reasoning: a document can have many chunks; retrieving 10× the desired
    // count ensures we can still return topK distinct *sources* after grouping.
    private const int FetchMultiplier = 10;

    private readonly IEmbeddingProvider              _embeddingProvider;
    private readonly IEmbeddingRepository            _embeddingRepository;
    private readonly ILogger<SemanticSearchService>  _logger;

    public SemanticSearchService(
        IEmbeddingProvider              embeddingProvider,
        IEmbeddingRepository            embeddingRepository,
        ILogger<SemanticSearchService>  logger)
    {
        _embeddingProvider   = embeddingProvider;
        _embeddingRepository = embeddingRepository;
        _logger              = logger;
    }

    public async Task<List<SemanticSearchResult>> SearchAsync(
        Guid                  userId,
        SemanticSearchRequest request,
        CancellationToken     cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return [];

        var queryVector = await _embeddingProvider.EmbedAsync(
            request.Query, cancellationToken);

        if (queryVector is null || queryVector.Length == 0)
        {
            _logger.LogWarning(
                "SemanticSearchService: EmbedAsync returned null for query — " +
                "LocalEmbeddingProvider may have failed to initialise. " +
                "Restart the backend; the model will re-initialise on next request.");
            return [];
        }

        var topK   = Math.Clamp(request.TopK, MinTopK, MaxTopK);
        var fetchK = topK * FetchMultiplier;

        var rows = await _embeddingRepository.SearchAsync(
            userId, queryVector, fetchK, cancellationToken);

        if (rows.Count == 0)
            _logger.LogInformation(
                "SemanticSearchService: SearchAsync returned 0 rows for user {UserId}. " +
                "Run POST /api/semantic-search/backfill to index your workspace.",
                userId);

        // 1. Apply optional source-type filter
        var filtered = request.SourceType.HasValue
            ? rows.Where(r => r.Record.SourceType == request.SourceType.Value)
            : rows;

        // 2. Deduplicate across chunks: keep only the best-scoring chunk per
        //    (SourceType, SourceId), then re-sort and limit to topK.
        return filtered
            .GroupBy(r => (r.Record.SourceType, r.Record.SourceId))
            .Select(g => g.OrderByDescending(r => r.Score).First())
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .Select(r => new SemanticSearchResult
            {
                SourceId   = r.Record.SourceId,
                SourceType = r.Record.SourceType,
                Title      = r.Record.Title ?? string.Empty,
                Score      = r.Score
            })
            .ToList();
    }
}
