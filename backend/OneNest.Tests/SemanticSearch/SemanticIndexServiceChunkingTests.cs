using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Services;
using OneNest.Application.Services;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;
using Xunit;

namespace OneNest.Tests.SemanticSearch;

/// <summary>
/// Tests for <see cref="SemanticIndexService"/> focusing on chunking behaviour,
/// CRUD safety when embedding fails, and content coverage beyond 8 000 characters.
/// </summary>
public class SemanticIndexServiceChunkingTests
{
    private static readonly Guid   UserId   = Guid.NewGuid();
    private static readonly Guid   SourceId = Guid.NewGuid();
    private static readonly float[] Vector  = Enumerable.Range(0, 384).Select(i => 0.01f * i).ToArray();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SemanticIndexService MakeService(
        Mock<IEmbeddingProvider>   embMock,
        Mock<IEmbeddingRepository> repoMock,
        Mock<ITextChunker>         chunkerMock) =>
        new(embMock.Object, repoMock.Object, chunkerMock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SemanticIndexService>.Instance);

    /// <summary>Chunker that returns N fixed chunks regardless of input.</summary>
    private static Mock<ITextChunker> ChunkerWithN(int n)
    {
        var m      = new Mock<ITextChunker>();
        var chunks = Enumerable.Range(0, n)
            .Select(i => $"Chunk {i} content.")
            .ToList<string>();
        m.Setup(c => c.Chunk(It.IsAny<string>())).Returns(chunks);
        return m;
    }

    // ── 1. Multiple chunks are upserted ──────────────────────────────────────

    [Fact]
    public async Task IndexAsync_MultipleChunks_UpsertsEachChunk()
    {
        var emb     = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vector);

        var repo    = new Mock<IEmbeddingRepository>();
        var chunker = ChunkerWithN(3);

        var svc = MakeService(emb, repo, chunker);
        await svc.IndexAsync(UserId, EmbeddingSourceType.Note, SourceId, "Title", "any text");

        // Upsert called once per chunk
        repo.Verify(r => r.UpsertAsync(It.IsAny<EmbeddingRecord>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // ── 2. ChunkIndex is sequential and zero-based ────────────────────────────

    [Fact]
    public async Task IndexAsync_ChunkIndexIsSequential()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vector);

        var capturedRecords = new List<EmbeddingRecord>();
        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.UpsertAsync(It.IsAny<EmbeddingRecord>(), It.IsAny<CancellationToken>()))
            .Callback<EmbeddingRecord, CancellationToken>((rec, _) => capturedRecords.Add(rec))
            .Returns(Task.CompletedTask);

        var svc = MakeService(emb, repo, ChunkerWithN(4));
        await svc.IndexAsync(UserId, EmbeddingSourceType.Document, SourceId, "Doc", "text");

        Assert.Equal(4, capturedRecords.Count);
        Assert.Equal([0, 1, 2, 3], capturedRecords.Select(r => r.ChunkIndex).ToArray());
    }

    // ── 3. Old chunks are deleted before re-indexing ─────────────────────────

    [Fact]
    public async Task IndexAsync_DeletesOldChunksFirst()
    {
        var emb  = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vector);

        var deleteCallOrder = new List<string>();
        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.DeleteBySourceAsync(UserId, EmbeddingSourceType.Note, SourceId, It.IsAny<CancellationToken>()))
            .Callback(() => deleteCallOrder.Add("delete"))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpsertAsync(It.IsAny<EmbeddingRecord>(), It.IsAny<CancellationToken>()))
            .Callback(() => deleteCallOrder.Add("upsert"))
            .Returns(Task.CompletedTask);

        var svc = MakeService(emb, repo, ChunkerWithN(2));
        await svc.IndexAsync(UserId, EmbeddingSourceType.Note, SourceId, "T", "text");

        // Delete must precede all upserts
        Assert.Equal("delete", deleteCallOrder[0]);
        Assert.All(deleteCallOrder.Skip(1), s => Assert.Equal("upsert", s));
    }

    // ── 4. Empty text → no delete, no upsert ─────────────────────────────────

    [Fact]
    public async Task IndexAsync_EmptyText_NoRepositoryCalls()
    {
        var repo    = new Mock<IEmbeddingRepository>();
        var chunker = new Mock<ITextChunker>();
        var svc     = MakeService(new Mock<IEmbeddingProvider>(), repo, chunker);

        await svc.IndexAsync(UserId, EmbeddingSourceType.Note, SourceId, "T", "   ");

        repo.Verify(r => r.DeleteBySourceAsync(It.IsAny<Guid>(), It.IsAny<EmbeddingSourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.UpsertAsync(It.IsAny<EmbeddingRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        chunker.Verify(c => c.Chunk(It.IsAny<string>()), Times.Never);
    }

    // ── 5. Embedding failure for one chunk skips that chunk (best-effort) ─────

    [Fact]
    public async Task IndexAsync_EmbeddingReturnsNullForOneChunk_OtherChunksUpserted()
    {
        int callCount = 0;
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(() =>
           {
               callCount++;
               return callCount == 2 ? null : Vector; // chunk 1 fails
           });

        var repo = new Mock<IEmbeddingRepository>();
        var svc  = MakeService(emb, repo, ChunkerWithN(3));

        await svc.IndexAsync(UserId, EmbeddingSourceType.Note, SourceId, "T", "text");

        // 3 chunks, 1 returns null → 2 upserts
        repo.Verify(r => r.UpsertAsync(It.IsAny<EmbeddingRecord>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── 6. Embedding exception swallowed — CRUD operation is not affected ─────

    [Fact]
    public async Task IndexAsync_EmbeddingThrows_DoesNotRethrow()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new InvalidOperationException("Model not loaded"));

        var repo = new Mock<IEmbeddingRepository>();
        var svc  = MakeService(emb, repo, ChunkerWithN(2));

        // Must not throw — best-effort
        var ex = await Record.ExceptionAsync(() =>
            svc.IndexAsync(UserId, EmbeddingSourceType.Note, SourceId, "T", "text"));

        Assert.Null(ex);
    }

    // ── 7. Repository exception swallowed — CRUD operation is not affected ────

    [Fact]
    public async Task IndexAsync_RepositoryUpsertThrows_DoesNotRethrow()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vector);

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.DeleteBySourceAsync(It.IsAny<Guid>(), It.IsAny<EmbeddingSourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpsertAsync(It.IsAny<EmbeddingRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var svc = MakeService(emb, repo, ChunkerWithN(1));

        var ex = await Record.ExceptionAsync(() =>
            svc.IndexAsync(UserId, EmbeddingSourceType.Note, SourceId, "T", "text"));

        Assert.Null(ex);
    }

    // ── 8. DeleteIndexAsync swallowed on exception ────────────────────────────

    [Fact]
    public async Task DeleteIndexAsync_RepositoryThrows_DoesNotRethrow()
    {
        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.DeleteBySourceAsync(It.IsAny<Guid>(), It.IsAny<EmbeddingSourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var svc = MakeService(new Mock<IEmbeddingProvider>(), repo, new Mock<ITextChunker>());

        var ex = await Record.ExceptionAsync(() =>
            svc.DeleteIndexAsync(UserId, EmbeddingSourceType.Note, SourceId));

        Assert.Null(ex);
    }

    // ── 9. Content beyond 8 000 chars is passed to the chunker ───────────────

    [Fact]
    public async Task IndexAsync_LongText_FullTextPassedToChunker()
    {
        // Build text significantly longer than old 8 000 char truncation limit
        var longText = string.Join(" ", Enumerable.Repeat("word", 3_000)); // ~15 000 chars

        string? capturedInput = null;
        var chunker = new Mock<ITextChunker>();
        chunker.Setup(c => c.Chunk(It.IsAny<string>()))
               .Callback<string>(t => capturedInput = t)
               .Returns(new List<string> { longText[..600], longText[600..] });

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vector);

        var repo = new Mock<IEmbeddingRepository>();
        var svc  = MakeService(emb, repo, chunker);

        await svc.IndexAsync(UserId, EmbeddingSourceType.Document, SourceId, "Doc", longText);

        // The FULL text (not a truncated version) must be passed to the chunker
        Assert.NotNull(capturedInput);
        Assert.Equal(longText.Length, capturedInput!.Length);
        Assert.True(capturedInput.Length > 8_000,
            $"Full text ({capturedInput.Length} chars) should exceed old 8 K truncation.");
    }

    // ── 10. All chunk records are associated with the correct UserId ──────────

    [Fact]
    public async Task IndexAsync_RecordsHaveCorrectUserId()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vector);

        var captured = new List<EmbeddingRecord>();
        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.UpsertAsync(It.IsAny<EmbeddingRecord>(), It.IsAny<CancellationToken>()))
            .Callback<EmbeddingRecord, CancellationToken>((r, _) => captured.Add(r))
            .Returns(Task.CompletedTask);

        var svc = MakeService(emb, repo, ChunkerWithN(3));
        await svc.IndexAsync(UserId, EmbeddingSourceType.Note, SourceId, "T", "text");

        Assert.All(captured, r => Assert.Equal(UserId, r.UserId));
    }
}
