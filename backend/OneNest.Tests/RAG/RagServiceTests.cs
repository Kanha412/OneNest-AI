using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using OneNest.Application.DTOs.AI;
using OneNest.Application.DTOs.Rag;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;
using OneNest.Infrastructure.AI;
using Xunit;

namespace OneNest.Tests.RAG;

/// <summary>
/// Unit tests for <see cref="RagService"/> (Phase 9 — RAG).
///
/// Coverage:
///   1-2.  Empty / whitespace query throws InvalidOperationException
///   3-4.  Embedding provider returns null or empty → NoSources response
///   5.    All results below similarity threshold → NoSources response
///   6.    Mixed scores → only above-threshold sources used
///   7.    Relevant note found → Note.Content fetched with correct userId
///   8.    Relevant document found → Document.ExtractedText fetched with correct userId
///   9.    Multiple chunks same source → best-scoring chunk wins (deduplication)
///   10.   TopK limits distinct sources returned
///   11.   TopK clamped to maximum (20)
///   12.   Repository always called with the caller's userId (cross-user isolation)
///   13.   NoteRepository fetched with the caller's userId (cross-user isolation)
///   14.   RagResponse.Sources contains correct SourceType and Title
///   15.   RagSourceDto does NOT expose an internal SourceId property
///   16.   Conversation history bounded to MaxConversationMessages
///   17.   Current query appended as last user message in conversation
///   18.   Gemini failure propagates exception (does not swallow)
///   19.   Gemini answer returned in RagResponse.Answer
///   20.   HasSources = true when sources found; false on NoSources path
/// </summary>
public class RagServiceTests
{
    // ── Shared test user ID ───────────────────────────────────────────────────
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>384-dim dummy vector (matches all-MiniLM-L6-v2 dimensions).</summary>
    private static float[] Vec(int dims = 384)
    {
        var v = new float[dims];
        for (var i = 0; i < dims; i++) v[i] = 0.01f * (i % 100);
        return v;
    }

    private static EmbeddingRecord MakeRecord(
        EmbeddingSourceType type,
        string              title,
        Guid?               sourceId   = null,
        int                 chunkIndex = 0) =>
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

    private static RagService BuildService(
        Mock<IEmbeddingProvider>?   embMock   = null,
        Mock<IEmbeddingRepository>? repoMock  = null,
        Mock<INoteRepository>?      noteMock  = null,
        Mock<IDocumentRepository>?  docMock   = null,
        Mock<IAIProvider>?          aiMock    = null,
        RagOptions?                 ragOpts   = null,
        AIOptions?                  aiOpts    = null)
    {
        embMock  ??= new Mock<IEmbeddingProvider>();
        repoMock ??= new Mock<IEmbeddingRepository>();
        noteMock ??= new Mock<INoteRepository>();
        docMock  ??= new Mock<IDocumentRepository>();
        aiMock   ??= new Mock<IAIProvider>();

        ragOpts ??= new RagOptions
        {
            TopK                   = 5,
            SimilarityThreshold    = 0.70,
            MaxContextCharacters   = 12_000,
            MaxConversationMessages = 10
        };
        aiOpts ??= new AIOptions { Model = "gemini-test" };

        return new RagService(
            embMock.Object,
            repoMock.Object,
            noteMock.Object,
            docMock.Object,
            aiMock.Object,
            Options.Create(ragOpts),
            Options.Create(aiOpts));
    }

    // ── Test 1: Empty query throws ────────────────────────────────────────────

    [Fact]
    public async Task AskAsync_EmptyQuery_ThrowsInvalidOperationException()
    {
        var svc = BuildService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AskAsync(UserId, new RagRequest { Query = "" }));
    }

    // ── Test 2: Whitespace query throws ──────────────────────────────────────

    [Fact]
    public async Task AskAsync_WhitespaceQuery_ThrowsInvalidOperationException()
    {
        var svc = BuildService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AskAsync(UserId, new RagRequest { Query = "   " }));
    }

    // ── Test 3: Embedding returns null → NoSources response ──────────────────

    [Fact]
    public async Task AskAsync_EmbeddingReturnsNull_ReturnsNoSourcesResponse()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((float[]?)null);

        var svc = BuildService(embMock: emb);

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "what are my notes about?" });

        Assert.False(result.HasSources);
        Assert.Empty(result.Sources);
        Assert.False(string.IsNullOrWhiteSpace(result.Answer)); // graceful fallback message
    }

    // ── Test 4: Embedding returns empty array → NoSources response ────────────

    [Fact]
    public async Task AskAsync_EmbeddingReturnsEmptyArray_ReturnsNoSourcesResponse()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync([]);

        var svc = BuildService(embMock: emb);

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "expenses last month" });

        Assert.False(result.HasSources);
        Assert.Empty(result.Sources);
    }

    // ── Test 5: All results below threshold → NoSources response ─────────────

    [Fact]
    public async Task AskAsync_AllResultsBelowThreshold_ReturnsNoSourcesResponse()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        // All rows score below 0.70 threshold
        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Note, "Low-score note"), 0.55),
            new(MakeRecord(EmbeddingSourceType.Note, "Another low"),    0.60),
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var svc = BuildService(embMock: emb, repoMock: repo,
            ragOpts: new RagOptions { SimilarityThreshold = 0.70 });

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "something obscure" });

        Assert.False(result.HasSources);
        Assert.Empty(result.Sources);
    }

    // ── Test 6: Mixed scores → only above-threshold sources used ─────────────

    [Fact]
    public async Task AskAsync_MixedScores_OnlyAboveThresholdIncluded()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        var goodId = Guid.NewGuid();
        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Note, "Good note",  goodId), 0.85), // above
            new(MakeRecord(EmbeddingSourceType.Note, "Bad note"),           0.45), // below
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var note = new Note { Id = goodId, UserId = UserId, Content = "Some note text" };
        var noteMock = new Mock<INoteRepository>();
        noteMock.Setup(n => n.GetByIdAsync(goodId, UserId)).ReturnsAsync(note);

        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("Answer from AI");

        var svc = BuildService(embMock: emb, repoMock: repo, noteMock: noteMock, aiMock: aiMock,
            ragOpts: new RagOptions { SimilarityThreshold = 0.70, TopK = 5, MaxContextCharacters = 12_000, MaxConversationMessages = 10 });

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "notes" });

        Assert.True(result.HasSources);
        Assert.Single(result.Sources);
        Assert.Equal("Good note", result.Sources[0].Title);
    }

    // ── Test 7: Relevant note → Note.Content fetched for context ─────────────

    [Fact]
    public async Task AskAsync_RelevantNote_FetchesNoteContentWithCorrectUserId()
    {
        var noteId = Guid.NewGuid();
        var emb    = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Note, "My Note", noteId), 0.90)
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var noteMock = new Mock<INoteRepository>();
        noteMock.Setup(n => n.GetByIdAsync(noteId, UserId))
                .ReturnsAsync(new Note { Id = noteId, UserId = UserId, Content = "Note content here" });

        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("AI answer");

        var svc = BuildService(embMock: emb, repoMock: repo, noteMock: noteMock, aiMock: aiMock);

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "note content" });

        // Verify note was fetched with the correct userId
        noteMock.Verify(n => n.GetByIdAsync(noteId, UserId), Times.Once);
        Assert.True(result.HasSources);
        Assert.Equal("Note", result.Sources[0].SourceType);
    }

    // ── Test 8: Relevant document → Document.ExtractedText fetched ───────────

    [Fact]
    public async Task AskAsync_RelevantDocument_FetchesDocumentExtractedTextWithCorrectUserId()
    {
        var docId = Guid.NewGuid();
        var emb   = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Document, "Resume", docId), 0.88)
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var docMock = new Mock<IDocumentRepository>();
        docMock.Setup(d => d.GetByIdAsync(docId, UserId))
               .ReturnsAsync(new Document
               {
                   Id            = docId,
                   UserId        = UserId,
                   Title         = "Resume",
                   ExtractedText = "10 years of experience in software engineering"
               });

        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("AI answer");

        var svc = BuildService(embMock: emb, repoMock: repo, docMock: docMock, aiMock: aiMock);

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "work experience" });

        docMock.Verify(d => d.GetByIdAsync(docId, UserId), Times.Once);
        Assert.True(result.HasSources);
        Assert.Equal("Document", result.Sources[0].SourceType);
    }

    // ── Test 9: Multiple chunks same source → best chunk wins ────────────────

    [Fact]
    public async Task AskAsync_MultipleChunksSameSource_KeepsBestChunkScore()
    {
        var sourceId = Guid.NewGuid();
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        // Three chunks from the same document with varying scores
        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Document, "Long doc", sourceId, chunkIndex: 0), 0.72),
            new(MakeRecord(EmbeddingSourceType.Document, "Long doc", sourceId, chunkIndex: 1), 0.95), // best
            new(MakeRecord(EmbeddingSourceType.Document, "Long doc", sourceId, chunkIndex: 2), 0.80),
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var docMock = new Mock<IDocumentRepository>();
        docMock.Setup(d => d.GetByIdAsync(sourceId, UserId))
               .ReturnsAsync(new Document { Id = sourceId, ExtractedText = "text" });

        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("answer");

        var svc = BuildService(embMock: emb, repoMock: repo, docMock: docMock, aiMock: aiMock);

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "long document" });

        // Only ONE source result (deduplication)
        Assert.Single(result.Sources);
        // Best chunk (index 1) selected
        Assert.Equal(1, result.Sources[0].ChunkIndex);
        // Document fetched only once
        docMock.Verify(d => d.GetByIdAsync(sourceId, UserId), Times.Once);
    }

    // ── Test 10: TopK limits distinct sources ─────────────────────────────────

    [Fact]
    public async Task AskAsync_TopKLimitsDistinctSourcesReturned()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        // 10 distinct notes, each with one chunk
        var rows = Enumerable.Range(0, 10)
            .Select(i => new EmbeddingSearchResult(
                MakeRecord(EmbeddingSourceType.Note, $"Note {i}"), 0.90 - i * 0.01))
            .ToList();

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var noteMock = new Mock<INoteRepository>();
        noteMock.Setup(n => n.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync((Guid id, Guid uid) => new Note { Id = id, Content = "text" });

        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("answer");

        var svc = BuildService(embMock: emb, repoMock: repo, noteMock: noteMock, aiMock: aiMock,
            ragOpts: new RagOptions { TopK = 3, SimilarityThreshold = 0.70, MaxContextCharacters = 12_000, MaxConversationMessages = 10 });

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "notes" });

        Assert.Equal(3, result.Sources.Count);
    }

    // ── Test 11: TopK clamped to maximum (20) ────────────────────────────────

    [Fact]
    public async Task AskAsync_TopKClampedToMax20()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        // 25 distinct notes
        var rows = Enumerable.Range(0, 25)
            .Select(i => new EmbeddingSearchResult(
                MakeRecord(EmbeddingSourceType.Note, $"Note {i}"), 0.90 - i * 0.005))
            .ToList();

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var noteMock = new Mock<INoteRepository>();
        noteMock.Setup(n => n.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync((Guid id, Guid uid) => new Note { Id = id, Content = "text" });

        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("answer");

        // Request TopK = 99 (above the maximum of 20)
        var svc = BuildService(embMock: emb, repoMock: repo, noteMock: noteMock, aiMock: aiMock,
            ragOpts: new RagOptions { TopK = 5, SimilarityThreshold = 0.0, MaxContextCharacters = 12_000, MaxConversationMessages = 10 });

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "all notes", TopK = 99 });

        Assert.True(result.Sources.Count <= 20,
            $"Expected ≤20 sources; got {result.Sources.Count}");
    }

    // ── Test 12: Repository called with caller's userId ───────────────────────

    [Fact]
    public async Task AskAsync_EmbeddingRepositoryCalledWithCallersUserId()
    {
        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        Guid? capturedUserId = null;
        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, float[], int, CancellationToken>((uid, _, _, _) => capturedUserId = uid)
            .ReturnsAsync(new List<EmbeddingSearchResult>());

        var svc = BuildService(embMock: emb, repoMock: repo);

        await svc.AskAsync(UserId, new RagRequest { Query = "test" });

        Assert.Equal(UserId, capturedUserId);
    }

    // ── Test 13: NoteRepository called with caller's userId ──────────────────

    [Fact]
    public async Task AskAsync_NoteRepositoryCalledWithCallersUserId_NotAnotherUser()
    {
        var noteId  = Guid.NewGuid();
        var otherUserId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Note, "My note", noteId), 0.90)
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        Guid? capturedNoteUserId = null;
        var noteMock = new Mock<INoteRepository>();
        noteMock.Setup(n => n.GetByIdAsync(noteId, UserId))
                .Callback<Guid, Guid>((_, uid) => capturedNoteUserId = uid)
                .ReturnsAsync(new Note { Id = noteId, Content = "text" });

        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("answer");

        var svc = BuildService(embMock: emb, repoMock: repo, noteMock: noteMock, aiMock: aiMock);

        await svc.AskAsync(UserId, new RagRequest { Query = "my note" });

        Assert.Equal(UserId, capturedNoteUserId);
        Assert.NotEqual(otherUserId, capturedNoteUserId);
    }

    // ── Test 14: Sources DTO has correct SourceType and Title ─────────────────

    [Fact]
    public async Task AskAsync_SourcesContainCorrectTypeAndTitle()
    {
        var noteId = Guid.NewGuid();
        var docId  = Guid.NewGuid();
        var emb    = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Note,     "Shopping list", noteId), 0.92),
            new(MakeRecord(EmbeddingSourceType.Document, "Tax return",    docId),  0.85),
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var noteMock = new Mock<INoteRepository>();
        noteMock.Setup(n => n.GetByIdAsync(noteId, UserId))
                .ReturnsAsync(new Note { Content = "Apples, milk" });

        var docMock = new Mock<IDocumentRepository>();
        docMock.Setup(d => d.GetByIdAsync(docId, UserId))
               .ReturnsAsync(new Document { ExtractedText = "Tax filing 2025" });

        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("answer");

        var svc = BuildService(embMock: emb, repoMock: repo, noteMock: noteMock, docMock: docMock, aiMock: aiMock);

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "what do I need to buy?" });

        Assert.Equal(2, result.Sources.Count);
        Assert.Equal("Note",           result.Sources[0].SourceType);
        Assert.Equal("Shopping list",  result.Sources[0].Title);
        Assert.Equal("Document",       result.Sources[1].SourceType);
        Assert.Equal("Tax return",     result.Sources[1].Title);
    }

    // ── Test 15: SourceId NOT exposed in RagSourceDto ─────────────────────────

    [Fact]
    public void RagSourceDto_DoesNotExposeSourceIdProperty()
    {
        var dtoType   = typeof(RagSourceDto);
        var sourceIdProp = dtoType.GetProperty("SourceId");

        // The SourceId must not exist on the public surface of RagSourceDto
        Assert.Null(sourceIdProp);
    }

    // ── Test 16: Conversation history bounded to MaxConversationMessages ──────

    [Fact]
    public async Task AskAsync_ConversationHistoryBoundedToMaxMessages()
    {
        // The repo must return at least one above-threshold result so that
        // the pipeline reaches the AI provider call and history is captured.
        var noteId = Guid.NewGuid();

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>
            {
                new(MakeRecord(EmbeddingSourceType.Note, "Some note", noteId), 0.90)
            });

        var noteMock = new Mock<INoteRepository>();
        noteMock.Setup(n => n.GetByIdAsync(noteId, UserId))
                .ReturnsAsync(new Note { Id = noteId, Content = "note text" });

        IReadOnlyList<ConversationMessage>? capturedHistory = null;
        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .Callback<string, IReadOnlyList<ConversationMessage>, CancellationToken>((_, h, _) => capturedHistory = h)
              .ReturnsAsync("answer");

        var svc = BuildService(embMock: emb, repoMock: repo, noteMock: noteMock, aiMock: aiMock,
            ragOpts: new RagOptions { TopK = 5, SimilarityThreshold = 0.0, MaxContextCharacters = 12_000, MaxConversationMessages = 3 });

        // Provide 7 prior turns (exceeds the cap of 3)
        var priorTurns = Enumerable.Range(1, 7)
            .Select(i => new ConversationMessage { Role = i % 2 == 0 ? "assistant" : "user", Content = $"Turn {i}" })
            .ToList();

        await svc.AskAsync(UserId, new RagRequest
        {
            Query                = "final question",
            ConversationMessages = priorTurns
        });

        // history = last 3 prior turns + 1 current query = 4 messages total
        Assert.NotNull(capturedHistory);
        Assert.Equal(4, capturedHistory!.Count); // 3 bounded prior + current query
    }

    // ── Test 17: Current query appended as last user message ─────────────────

    [Fact]
    public async Task AskAsync_CurrentQueryAppendedAsLastUserMessageInHistory()
    {
        const string query  = "what are my upcoming tasks?";
        var          noteId = Guid.NewGuid();

        var emb = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        // At least one above-threshold result is needed to reach the AI call.
        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmbeddingSearchResult>
            {
                new(MakeRecord(EmbeddingSourceType.Note, "Task note", noteId), 0.88)
            });

        var noteMock = new Mock<INoteRepository>();
        noteMock.Setup(n => n.GetByIdAsync(noteId, UserId))
                .ReturnsAsync(new Note { Id = noteId, Content = "Buy groceries" });

        IReadOnlyList<ConversationMessage>? capturedHistory = null;
        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .Callback<string, IReadOnlyList<ConversationMessage>, CancellationToken>((_, h, _) => capturedHistory = h)
              .ReturnsAsync("answer");

        var svc = BuildService(embMock: emb, repoMock: repo, noteMock: noteMock, aiMock: aiMock);

        await svc.AskAsync(UserId, new RagRequest { Query = query });

        Assert.NotNull(capturedHistory);
        var last = capturedHistory!.Last();
        Assert.Equal("user", last.Role);
        Assert.Equal(query,  last.Content);
    }

    // ── Test 18: Gemini failure propagates (not swallowed) ────────────────────

    [Fact]
    public async Task AskAsync_GeminiFailure_PropagatesException()
    {
        var noteId = Guid.NewGuid();
        var emb    = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Note, "Some note", noteId), 0.90)
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var noteMock = new Mock<INoteRepository>();
        noteMock.Setup(n => n.GetByIdAsync(noteId, UserId))
                .ReturnsAsync(new Note { Content = "text" });

        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("Gemini rate limit exceeded."));

        var svc = BuildService(embMock: emb, repoMock: repo, noteMock: noteMock, aiMock: aiMock);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AskAsync(UserId, new RagRequest { Query = "tell me about my notes" }));
    }

    // ── Test 19: Gemini answer returned in RagResponse.Answer ────────────────

    [Fact]
    public async Task AskAsync_GeminiAnswer_ReturnedInResponseAnswer()
    {
        const string expectedAnswer = "Based on your notes, the meeting is on Friday at 3pm.";

        var noteId = Guid.NewGuid();
        var emb    = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        var rows = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Note, "Meeting note", noteId), 0.88)
        };

        var repo = new Mock<IEmbeddingRepository>();
        repo.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var noteMock = new Mock<INoteRepository>();
        noteMock.Setup(n => n.GetByIdAsync(noteId, UserId))
                .ReturnsAsync(new Note { Content = "Team meeting Friday 3pm conference room B" });

        var aiMock = new Mock<IAIProvider>();
        aiMock.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(expectedAnswer);

        var svc = BuildService(embMock: emb, repoMock: repo, noteMock: noteMock, aiMock: aiMock);

        var result = await svc.AskAsync(UserId, new RagRequest { Query = "when is the meeting?" });

        Assert.Equal(expectedAnswer, result.Answer);
    }

    // ── Test 20: HasSources reflects whether sources were found ───────────────

    [Fact]
    public async Task AskAsync_HasSources_TrueWhenSourcesFound_FalseOnNoSourcesPath()
    {
        // -- Part A: sources found → HasSources = true -----------------------
        var noteId = Guid.NewGuid();
        var emb    = new Mock<IEmbeddingProvider>();
        emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Vec());

        var rowsWithSource = new List<EmbeddingSearchResult>
        {
            new(MakeRecord(EmbeddingSourceType.Note, "Budget note", noteId), 0.90)
        };

        var repoA = new Mock<IEmbeddingRepository>();
        repoA.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(rowsWithSource);

        var noteA = new Mock<INoteRepository>();
        noteA.Setup(n => n.GetByIdAsync(noteId, UserId))
             .ReturnsAsync(new Note { Content = "Budget is ₹50000" });

        var aiA = new Mock<IAIProvider>();
        aiA.Setup(a => a.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationMessage>>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync("Here is the budget info...");

        var svcA = BuildService(embMock: emb, repoMock: repoA, noteMock: noteA, aiMock: aiA);
        var resultA = await svcA.AskAsync(UserId, new RagRequest { Query = "what is my budget?" });

        Assert.True(resultA.HasSources);
        Assert.NotEmpty(resultA.Sources);

        // -- Part B: no relevant results → HasSources = false -----------------
        var repoB = new Mock<IEmbeddingRepository>();
        repoB.Setup(r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<EmbeddingSearchResult>()); // no hits

        var svcB = BuildService(embMock: emb, repoMock: repoB);
        var resultB = await svcB.AskAsync(UserId, new RagRequest { Query = "something with no results" });

        Assert.False(resultB.HasSources);
        Assert.Empty(resultB.Sources);
        Assert.False(string.IsNullOrWhiteSpace(resultB.Answer)); // fallback message present
    }
}
