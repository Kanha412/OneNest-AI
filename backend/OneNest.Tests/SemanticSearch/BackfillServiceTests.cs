using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Services;
using OneNest.Application.Services;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;
using Xunit;

namespace OneNest.Tests.SemanticSearch;

/// <summary>
/// Unit tests for <see cref="BackfillService"/>.
/// </summary>
public class BackfillServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Note MakeNote(string title = "Note", string? content = "Content")
        => new()
        {
            Id      = Guid.NewGuid(),
            UserId  = UserId,
            Title   = title,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

    private static Document MakeDoc(string title = "Doc", string? extractedText = "Extracted text")
        => new()
        {
            Id            = Guid.NewGuid(),
            UserId        = UserId,
            Title         = title,
            ExtractedText = extractedText,
            CreatedAt     = DateTime.UtcNow
        };

    private static BackfillService MakeService(
        Mock<INoteRepository>       noteRepo,
        Mock<IDocumentRepository>   docRepo,
        Mock<ISemanticIndexService> indexSvc)
        => new(noteRepo.Object, docRepo.Object, indexSvc.Object,
               NullLogger<BackfillService>.Instance);

    // ── 1. Notes are indexed ──────────────────────────────────────────────────

    [Fact]
    public async Task BackfillUserAsync_WithNotes_IndexesAllNotes()
    {
        var noteRepo  = new Mock<INoteRepository>();
        noteRepo.Setup(r => r.GetAllAsync(UserId))
                .ReturnsAsync(new List<Note> { MakeNote("Note A"), MakeNote("Note B") });

        var docRepo   = new Mock<IDocumentRepository>();
        docRepo.Setup(r => r.GetAllAsync(UserId))
               .ReturnsAsync(new List<Document>());

        var indexSvc  = new Mock<ISemanticIndexService>();
        var svc       = MakeService(noteRepo, docRepo, indexSvc);

        var result = await svc.BackfillUserAsync(UserId);

        Assert.Equal(2, result.NotesIndexed);
        Assert.Equal(0, result.DocumentsIndexed);
        Assert.Equal(0, result.Errors);

        indexSvc.Verify(
            s => s.IndexAsync(UserId, EmbeddingSourceType.Note, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── 2. Documents with extracted text are indexed ──────────────────────────

    [Fact]
    public async Task BackfillUserAsync_WithDocuments_IndexesDocsWithExtractedText()
    {
        var noteRepo = new Mock<INoteRepository>();
        noteRepo.Setup(r => r.GetAllAsync(UserId)).ReturnsAsync(new List<Note>());

        var docRepo = new Mock<IDocumentRepository>();
        docRepo.Setup(r => r.GetAllAsync(UserId))
               .ReturnsAsync(new List<Document>
               {
                   MakeDoc("Report",  "Full text of the report..."),
                   MakeDoc("Receipt", "Item: coffee. Amount: 3.50")
               });

        var indexSvc = new Mock<ISemanticIndexService>();
        var svc      = MakeService(noteRepo, docRepo, indexSvc);

        var result = await svc.BackfillUserAsync(UserId);

        Assert.Equal(0, result.NotesIndexed);
        Assert.Equal(2, result.DocumentsIndexed);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Errors);
    }

    // ── 3. Documents with no extracted text are skipped (not errors) ──────────

    [Fact]
    public async Task BackfillUserAsync_DocumentWithNoExtractedText_IsSkipped()
    {
        var noteRepo = new Mock<INoteRepository>();
        noteRepo.Setup(r => r.GetAllAsync(UserId)).ReturnsAsync(new List<Note>());

        var docRepo = new Mock<IDocumentRepository>();
        docRepo.Setup(r => r.GetAllAsync(UserId))
               .ReturnsAsync(new List<Document>
               {
                   MakeDoc("Image PDF", extractedText: null),
                   MakeDoc("Empty doc", extractedText: "   ")
               });

        var indexSvc = new Mock<ISemanticIndexService>();
        var svc      = MakeService(noteRepo, docRepo, indexSvc);

        var result = await svc.BackfillUserAsync(UserId);

        Assert.Equal(0, result.DocumentsIndexed);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(0, result.Errors);

        indexSvc.Verify(
            s => s.IndexAsync(It.IsAny<Guid>(), EmbeddingSourceType.Document, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── 4. Per-item exception is swallowed; run still completes ───────────────

    [Fact]
    public async Task BackfillUserAsync_OneItemFails_RestillRuns()
    {
        var notes = new List<Note> { MakeNote("A"), MakeNote("B"), MakeNote("C") };

        var noteRepo = new Mock<INoteRepository>();
        noteRepo.Setup(r => r.GetAllAsync(UserId)).ReturnsAsync(notes);

        var docRepo = new Mock<IDocumentRepository>();
        docRepo.Setup(r => r.GetAllAsync(UserId)).ReturnsAsync(new List<Document>());

        int callCount = 0;
        var indexSvc  = new Mock<ISemanticIndexService>();
        indexSvc.Setup(s => s.IndexAsync(It.IsAny<Guid>(), EmbeddingSourceType.Note, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    callCount++;
                    return callCount == 2
                        ? Task.FromException(new Exception("simulated failure"))
                        : Task.CompletedTask;
                });

        var svc    = MakeService(noteRepo, docRepo, indexSvc);
        var result = await svc.BackfillUserAsync(UserId);

        Assert.Equal(2, result.NotesIndexed); // A and C succeed
        Assert.Equal(1, result.Errors);       // B fails
    }

    // ── 5. Notes with null content are still indexed (title only) ─────────────

    [Fact]
    public async Task BackfillUserAsync_NoteWithNullContent_IndexedByTitleOnly()
    {
        var noteRepo = new Mock<INoteRepository>();
        noteRepo.Setup(r => r.GetAllAsync(UserId))
                .ReturnsAsync(new List<Note> { MakeNote("My Note", content: null) });

        var docRepo = new Mock<IDocumentRepository>();
        docRepo.Setup(r => r.GetAllAsync(UserId)).ReturnsAsync(new List<Document>());

        string? capturedText = null;
        var indexSvc = new Mock<ISemanticIndexService>();
        indexSvc.Setup(s => s.IndexAsync(UserId, EmbeddingSourceType.Note, It.IsAny<Guid>(), "My Note", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, EmbeddingSourceType, Guid, string, string, CancellationToken>(
                    (_, _, _, _, text, _) => capturedText = text)
                .Returns(Task.CompletedTask);

        var svc    = MakeService(noteRepo, docRepo, indexSvc);
        var result = await svc.BackfillUserAsync(UserId);

        Assert.Equal(1, result.NotesIndexed);
        Assert.NotNull(capturedText);
        Assert.Contains("My Note", capturedText);
    }

    // ── 6. Cross-user isolation: only the caller's items are indexed ──────────

    [Fact]
    public async Task BackfillUserAsync_OnlyIndexesItemsForRequestedUser()
    {
        var targetUser = Guid.NewGuid();
        var otherUser  = Guid.NewGuid();

        var noteRepo = new Mock<INoteRepository>();
        // Returns notes for targetUser only (repository is already user-scoped)
        noteRepo.Setup(r => r.GetAllAsync(targetUser))
                .ReturnsAsync(new List<Note> { MakeNote() });
        noteRepo.Setup(r => r.GetAllAsync(otherUser))
                .ReturnsAsync(new List<Note>()); // separate call, empty result

        var docRepo = new Mock<IDocumentRepository>();
        docRepo.Setup(r => r.GetAllAsync(It.IsAny<Guid>()))
               .ReturnsAsync(new List<Document>());

        var indexSvc = new Mock<ISemanticIndexService>();
        var svc      = MakeService(noteRepo, docRepo, indexSvc);

        await svc.BackfillUserAsync(targetUser);

        // Verify the repository was only called with the target userId
        noteRepo.Verify(r => r.GetAllAsync(targetUser), Times.Once);
        noteRepo.Verify(r => r.GetAllAsync(It.Is<Guid>(g => g != targetUser)), Times.Never);

        // And IndexAsync is called with targetUser only
        indexSvc.Verify(
            s => s.IndexAsync(
                It.Is<Guid>(g => g != targetUser),
                It.IsAny<EmbeddingSourceType>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── 7. Result counters are accurate ──────────────────────────────────────

    [Fact]
    public async Task BackfillUserAsync_MixedData_CountsAreCorrect()
    {
        var noteRepo = new Mock<INoteRepository>();
        noteRepo.Setup(r => r.GetAllAsync(UserId))
                .ReturnsAsync(new List<Note> { MakeNote(), MakeNote() }); // 2 notes

        var docRepo = new Mock<IDocumentRepository>();
        docRepo.Setup(r => r.GetAllAsync(UserId))
               .ReturnsAsync(new List<Document>
               {
                   MakeDoc("D1", "text"),   // indexed
                   MakeDoc("D2", null),     // skipped
                   MakeDoc("D3", "text")    // indexed
               });

        var indexSvc = new Mock<ISemanticIndexService>();
        var svc      = MakeService(noteRepo, docRepo, indexSvc);

        var result = await svc.BackfillUserAsync(UserId);

        Assert.Equal(2, result.NotesIndexed);
        Assert.Equal(2, result.DocumentsIndexed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Errors);
    }
}
