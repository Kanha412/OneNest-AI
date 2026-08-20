using Microsoft.Extensions.Options;
using OneNest.Application.DTOs.AI;
using OneNest.Application.DTOs.Rag;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Services;
using OneNest.Application.Services;
using OneNest.Domain.Enums;

namespace OneNest.Infrastructure.AI;

/// <summary>
/// Phase 9 — Retrieval-Augmented Generation service.
///
/// Pipeline per request:
///   1. Validate the query (non-empty).
///   2. Embed the query using the configured <see cref="IEmbeddingProvider"/>
///      (local ONNX or Gemini — whichever Phase 8 uses).
///   3. Fetch raw chunk candidates from pgvector via <see cref="IEmbeddingRepository"/>.
///   4. Apply similarity threshold; deduplicate by source (best chunk per source);
///      limit to TopK distinct sources.
///   5. Fetch the source text for each winner (Note.Content / Document.ExtractedText).
///   6. Build a bounded, injection-safe system prompt via <see cref="RagContextBuilder"/>.
///   7. Assemble conversation history (bounded) plus the current query as the
///      final user turn.
///   8. Call <see cref="IAIProvider"/> (Gemini) for the grounded answer.
///   9. Return <see cref="RagResponse"/> with answer + source citations
///      (SourceId is intentionally not exposed).
///
/// <b>User isolation:</b> every repository call is scoped by the <c>userId</c>
/// parameter that is resolved from the JWT by the controller — the client
/// can never supply it.
///
/// <b>Backward compatibility:</b> this service is entirely additive.
/// <see cref="ISemanticSearchService"/>, <see cref="IAIConversationService"/>,
/// <see cref="ISemanticIndexService"/>, and the embedding infrastructure are
/// all untouched.
/// </summary>
public class RagService : IRagService
{
    private const int MinTopK = 1;
    private const int MaxTopK = 20;

    // Same 10× over-fetch multiplier as SemanticSearchService so the
    // deduplication step has enough raw candidates to fill topK *distinct* sources.
    private const int FetchMultiplier = 10;

    private static readonly string NoSourcesAnswer =
        "I couldn't find any relevant content in your notes or documents for that query. " +
        "Make sure you have added notes or uploaded documents with searchable text, " +
        "or try rephrasing your question.";

    private readonly IEmbeddingProvider   _embeddingProvider;
    private readonly IEmbeddingRepository _embeddingRepository;
    private readonly INoteRepository      _noteRepository;
    private readonly IDocumentRepository  _documentRepository;
    private readonly IAIProvider          _aiProvider;
    private readonly RagOptions           _ragOptions;
    private readonly AIOptions            _aiOptions;

    public RagService(
        IEmbeddingProvider          embeddingProvider,
        IEmbeddingRepository        embeddingRepository,
        INoteRepository             noteRepository,
        IDocumentRepository         documentRepository,
        IAIProvider                 aiProvider,
        IOptions<RagOptions>        ragOptions,
        IOptions<AIOptions>         aiOptions)
    {
        _embeddingProvider   = embeddingProvider;
        _embeddingRepository = embeddingRepository;
        _noteRepository      = noteRepository;
        _documentRepository  = documentRepository;
        _aiProvider          = aiProvider;
        _ragOptions          = ragOptions.Value;
        _aiOptions           = aiOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<RagResponse> AskAsync(
        Guid              userId,
        RagRequest        request,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Validate ───────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new InvalidOperationException("Query cannot be empty.");

        // ── 2. Embed query ────────────────────────────────────────────────────
        var queryVector = await _embeddingProvider.EmbedAsync(request.Query, cancellationToken);
        if (queryVector is null || queryVector.Length == 0)
            return BuildNoSourcesResponse();

        // ── 3. Fetch raw chunk candidates from pgvector ───────────────────────
        var topK      = Math.Clamp(request.TopK ?? _ragOptions.TopK, MinTopK, MaxTopK);
        var threshold = request.SimilarityThreshold ?? _ragOptions.SimilarityThreshold;
        var fetchK    = topK * FetchMultiplier;

        var rows = await _embeddingRepository.SearchAsync(
            userId, queryVector, fetchK, cancellationToken);

        // ── 4. Filter, deduplicate, limit ─────────────────────────────────────
        // Keep only the best-scoring chunk per (SourceType, SourceId); then topK.
        var bestPerSource = rows
            .Where(r => r.Score >= threshold)
            .GroupBy(r => (r.Record.SourceType, r.Record.SourceId))
            .Select(g => g.OrderByDescending(r => r.Score).First())
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        if (bestPerSource.Count == 0)
            return BuildNoSourcesResponse();

        // ── 5. Fetch source text (user-scoped) ────────────────────────────────
        var ragChunks = new List<RagChunk>(bestPerSource.Count);

        foreach (var row in bestPerSource)
        {
            string? text = null;

            if (row.Record.SourceType == EmbeddingSourceType.Note)
            {
                // GetByIdAsync(id, userId) — user-scoped at the repository level
                var note = await _noteRepository.GetByIdAsync(
                    row.Record.SourceId, userId);
                text = note?.Content;
            }
            else
            {
                var doc = await _documentRepository.GetByIdAsync(
                    row.Record.SourceId, userId);
                text = doc?.ExtractedText;
            }

            ragChunks.Add(new RagChunk(
                SourceType: row.Record.SourceType.ToString(),
                Title:      row.Record.Title ?? string.Empty,
                Text:       text ?? string.Empty,
                Score:      row.Score,
                ChunkIndex: row.Record.ChunkIndex));
        }

        // ── 6. Build bounded, injection-safe system prompt ────────────────────
        var systemPrompt = RagContextBuilder.Build(ragChunks, _ragOptions.MaxContextCharacters);

        // ── 7. Assemble conversation history ──────────────────────────────────
        // Prior turns (bounded) + the current query as the final user message.
        var maxMsgs = _ragOptions.MaxConversationMessages;

        var history = new List<ConversationMessage>(
            (request.ConversationMessages ?? []).TakeLast(maxMsgs));

        history.Add(new ConversationMessage
        {
            Role    = "user",
            Content = request.Query.Trim()
        });

        // ── 8. Generate grounded answer ───────────────────────────────────────
        var answer = await _aiProvider.GenerateResponseAsync(systemPrompt, history, cancellationToken);

        // ── 9. Build response (SourceId intentionally omitted) ────────────────
        var sources = bestPerSource
            .Select(r => new RagSourceDto
            {
                SourceType = r.Record.SourceType.ToString(),
                Title      = r.Record.Title ?? string.Empty,
                ChunkIndex = r.Record.ChunkIndex
            })
            .ToList();

        return new RagResponse
        {
            Answer     = answer?.Trim() ?? string.Empty,
            Sources    = sources,
            HasSources = true,
            Model      = _aiOptions.Model ?? string.Empty,
            Timestamp  = DateTime.UtcNow
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private RagResponse BuildNoSourcesResponse() => new()
    {
        Answer     = NoSourcesAnswer,
        Sources    = [],
        HasSources = false,
        Model      = _aiOptions.Model ?? string.Empty,
        Timestamp  = DateTime.UtcNow
    };
}
