using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using OneNest.Application.DTOs.SemanticSearch;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Services;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;
using Xunit;

namespace OneNest.Tests.SemanticSearch;

/// <summary>
/// Unit tests for <see cref="SemanticSearchService"/> (Phase 8).
/// Covers: empty query, embedding unavailable, result mapping, source-type
/// filter, TopK clamping, SourceId propagation, chunk deduplication, and
/// cross-user isolation.
/// </summary>
public class SemanticSearchServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>384 dimensions to match all-MiniLM-L6-v2.</summary>
    private static float[] DummyVector(int dims = 384)
    {
        var v = new float[dims];
        for (var i = 0; i < dims; i++) v[i] = 0.01f * (i % 100);
        return v;
    }

    private static EmbeddingRecord MakeRecord(
        EmbeddingSourceType type,
        string title,
        Guid? sourceId  = null,
        int   chunkIndex = 0) =>
        new()
        {
            Id         = Guid.NewGuid(),
            UserId     = UserId,
            SourceType = type,
            SourceId   = sourceId ?? Guid.NewGuid(),
            Title      = title,
            ChunkIndex = chunkIndex,
            Embedding  = [],
            CreatedAt  = DateTime.UtcNow
        };

    private static SemanticSearchService MakeService(
        Mock<IEmbeddingProvider>   embMock,
        Mock<IEmbeddingRepository> repoMock) =>
        new(embMock.Object, repoMock.Object);

    // ── 1. Empty query returns empty list ────────────────────────────────────

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        var svc = MakeService(new Mock<IEmbeddingProvider>(), new Mock<IEmbeddingRepository>());

        var results = await svc.SearchAsync(UserId, new SemanticSearchRequest { Query = "  " });

        Assert.Empty(results);
    }

    // ── 2. Embedding provider returns null → empty list ──────────────────────

    [Fact]
    public async Task SearchAsync_EmbeddingUnavailable_ReturnsEmpty()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((float[]?)null);

        var svc = MakeService(emb, new Mock<IEmbeddingRepository>());

        var results = await svc.SearchAsync(UserId, new SemanticSearchRequest { Query = "notes about meetings" });

        Assert.Empty(results);
    }

    // ── 3. Results are returned and mapped correctly ──────────────────────────

    [Fact]
    public async Task SearchAsync_ValidQuery_ReturnsMappedResults()
    {
        var vector = DummyVector();

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(vector);

        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Note,     "Meeting notes"), 0.91),
            new(MakeRecord(EmbeddingSourceType.Document, "My resume"),     0.82)
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(UserId, vector, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var svc = MakeService(emb, repo);

        var results = await svc.SearchAsync(UserId, new SemanticSearchRequest { Query = "meetings", TopK = 5 });

        Assert.Equal(2, results.Count);
        Assert.Equal("Meeting notes", results[0].Title);
        Assert.Equal(0.91, results[0].Score, 2);
        Assert.Equal("My resume",     results[1].Title);
    }

    // ── 4. SourceType filter works ────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_SourceTypeFilter_OnlyReturnsMatchingType()
    {
        var vector = DummyVector();

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(vector);

        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Note,     "A note"),     0.95),
            new(MakeRecord(EmbeddingSourceType.Document, "A document"), 0.88)
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var svc = MakeService(emb, repo);

        var results = await svc.SearchAsync(UserId, new SemanticSearchRequest
        {
            Query      = "something",
            TopK       = 5,
            SourceType = EmbeddingSourceType.Note
        });

        Assert.Single(results);
        Assert.Equal(EmbeddingSourceType.Note, results[0].SourceType);
    }

    // ── 5. TopK is clamped to [1, 20] ────────────────────────────────────────

    [Theory]
    [InlineData(0,  1)]   // below min → clamped to 1
    [InlineData(25, 20)]  // above max → clamped to 20
    [InlineData(5,  5)]   // within range → unchanged
    public async Task SearchAsync_TopKClamped(int requestedTopK, int expectedMaxResults)
    {
        var vector = DummyVector();

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(vector);

        // Build 25 unique-source records (one chunk each)
        var rows = Enumerable.Range(0, 25)
            .Select(i => new EmbeddingSearchResult(
                MakeRecord(EmbeddingSourceType.Note, $"Note {i}"),
                0.9 - i * 0.01))
            .ToList();

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var svc = MakeService(emb, repo);

        var results = await svc.SearchAsync(UserId, new SemanticSearchRequest
        {
            Query = "test",
            TopK  = requestedTopK
        });

        Assert.True(results.Count <= expectedMaxResults,
            $"Expected ≤{expectedMaxResults} results, got {results.Count}");
    }

    // ── 6. SourceId is correctly propagated ──────────────────────────────────

    [Fact]
    public async Task SearchAsync_SourceIdPropagatedToResult()
    {
        var vector   = DummyVector();
        var sourceId = Guid.NewGuid();

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(vector);

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>
            {
                new(MakeRecord(EmbeddingSourceType.Document, "My doc", sourceId), 0.99)
            });

        var results = await MakeService(emb, repo)
            .SearchAsync(UserId, new SemanticSearchRequest { Query = "doc" });

        Assert.Single(results);
        Assert.Equal(sourceId, results[0].SourceId);
    }

    // ── 7. Multiple chunks from the same source deduplicate to one result ─────

    [Fact]
    public async Task SearchAsync_MultipleChunksSameSource_DeduplicatesToOneResult()
    {
        var vector   = DummyVector();
        var sourceId = Guid.NewGuid();

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(vector);

        // Three chunks from the same source with different scores
        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Document, "Long report", sourceId, chunkIndex: 0), 0.70),
            new(MakeRecord(EmbeddingSourceType.Document, "Long report", sourceId, chunkIndex: 1), 0.92), // best
            new(MakeRecord(EmbeddingSourceType.Document, "Long report", sourceId, chunkIndex: 2), 0.81),
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var results = await MakeService(emb, repo)
            .SearchAsync(UserId, new SemanticSearchRequest { Query = "report", TopK = 5 });

        // Only ONE result for that source
        Assert.Single(results);
        Assert.Equal(sourceId, results[0].SourceId);
        // The best-scoring chunk's score is surfaced
        Assert.Equal(0.92, results[0].Score, 2);
    }

    // ── 8. Deduplication keeps best chunk when sources are mixed ─────────────

    [Fact]
    public async Task SearchAsync_MixedSourcesWithMultipleChunks_DeduplicatesCorrectly()
    {
        var vector  = DummyVector();
        var docId   = Guid.NewGuid();
        var noteId  = Guid.NewGuid();

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(vector);

        var rows = new List<EmbeddingSearchResult>
        {
            // Document: 3 chunks
            new(MakeRecord(EmbeddingSourceType.Document, "Doc", docId,  0), 0.60),
            new(MakeRecord(EmbeddingSourceType.Document, "Doc", docId,  1), 0.88),
            new(MakeRecord(EmbeddingSourceType.Document, "Doc", docId,  2), 0.75),
            // Note: 1 chunk
            new(MakeRecord(EmbeddingSourceType.Note,     "Note", noteId, 0), 0.95),
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var results = await MakeService(emb, repo)
            .SearchAsync(UserId, new SemanticSearchRequest { Query = "q", TopK = 10 });

        // Two unique sources
        Assert.Equal(2, results.Count);
        // Ordered by best score descending: Note (0.95) then Doc (0.88)
        Assert.Equal(noteId, results[0].SourceId);
        Assert.Equal(0.95,   results[0].Score, 2);
        Assert.Equal(docId,  results[1].SourceId);
        Assert.Equal(0.88,   results[1].Score, 2);
    }

    // ── 9. Cross-user isolation: repository is called with the correct userId ─

    [Fact]
    public async Task SearchAsync_UsesCorrectUserId_NotOtherUser()
    {
        var userA   = Guid.NewGuid();
        var userB   = Guid.NewGuid();
        var vector  = DummyVector();

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(vector);

        Guid? capturedUserId = null;
        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, float[], int, CancellationToken>((uid, _, _, _) => capturedUserId = uid)
            .ReturnsAsync(new List<EmbeddingSearchResult>());

        var svc = MakeService(emb, repo);

        await svc.SearchAsync(userA, new SemanticSearchRequest { Query = "test" });

        Assert.Equal(userA, capturedUserId);
        Assert.NotEqual(userB, capturedUserId);
    }
}
