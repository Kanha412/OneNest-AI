using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using OneNest.Application.DTOs.AI;
using OneNest.Application.DTOs.Documents;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Enums;
using OneNest.Infrastructure.AI.WorkspaceTools;
using Xunit;

namespace OneNest.Tests.AI;

/// <summary>
/// Unit tests for DocumentsWorkspaceTool (Phase 6).
///
/// Key behaviours under test:
///   1. medium depth, no summary  → ExtractedText snippet
///   2. medium depth, has summary, general question → AISummary (preferred, lightweight)
///   3. medium depth, has summary, DETAIL question  → ExtractedText + summary note
///   4. high depth                → ExtractedText (full) + summary note
///   5. low depth                 → stats only; no content ever
///   6. CanHandle keyword routing
///   7. IsDetailQuestion detection
/// </summary>
public class DocumentsWorkspaceToolTests
{
    // ── Test fixtures ────────────────────────────────────────────────────────

    private static DocumentSummaryResponse MakeSummary(IEnumerable<DocumentResponse> recent) =>
        new()
        {
            TotalDocuments = 1,
            TodayUploads   = 1,
            StorageUsed    = 1024,
            RecentDocuments       = new List<DocumentResponse>(recent),
            CategoryDistribution  = new()
        };

    private static DocumentResponse Doc(
        Guid   id,
        string title,
        bool   isTextExtracted = true,
        string? aiSummary      = null) =>
        new()
        {
            Id              = id,
            Title           = title,
            OriginalFileName = "test.txt",
            ContentType     = "text/plain",
            FileSize        = 512,
            Category        = DocumentCategory.Personal,
            Description     = string.Empty,
            IsTextExtracted  = isTextExtracted,
            AISummary        = aiSummary,
            CreatedAt        = DateTime.UtcNow
        };

    private static WorkspaceToolExecutionContext Ctx(string depth, string prompt = "Tell me about my document") =>
        new()
        {
            UserPrompt = prompt,
            History    = Array.Empty<ConversationMessage>(),
            UtcNow     = DateTime.UtcNow,
            ContextDepth = depth
        };

    // ── 1. medium, no summary → ExtractedText snippet ────────────────────────

    [Fact]
    public async Task Medium_NoSummary_InjectsExtractedText()
    {
        var id  = Guid.NewGuid();
        var doc = Doc(id, "MyDoc", isTextExtracted: true, aiSummary: null);
        const string text = "My name is Kanha. I am a software developer.";

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));
        svc.Setup(s => s.GetExtractedTextAsync(id)).ReturnsAsync(text);

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("medium"));

        Assert.Contains("My name is Kanha", result!.Summary);
        Assert.Contains("extracted text preview", result.Summary);
        svc.Verify(s => s.GetExtractedTextAsync(id), Times.Once);
    }

    // ── 2. medium, has summary, general question → prefer summary ────────────

    [Fact]
    public async Task Medium_HasSummary_GeneralQuestion_UsesSummary()
    {
        var id  = Guid.NewGuid();
        var doc = Doc(id, "Resume", isTextExtracted: true,
                       aiSummary: "Experienced software developer specialising in .NET.");

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("medium", "Tell me about my resume"));

        Assert.Contains("Experienced software developer", result!.Summary);
        Assert.Contains("AI summary", result.Summary);
        // Must NOT hit DB for raw text on a general question
        svc.Verify(s => s.GetExtractedTextAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ── 3. medium, has summary, DETAIL question → ExtractedText + summary note

    [Fact]
    public async Task Medium_HasSummary_DetailQuestion_UsesExtractedText()
    {
        var id  = Guid.NewGuid();
        var doc = Doc(id, "Resume", isTextExtracted: true,
                       aiSummary: "Experienced developer; experience duration not specified.");

        const string rawText =
            "OneNest Corp — Senior Dev — Jan 2020 to Jan 2023 (3 years)\n" +
            "Accenture — Dev — Aug 2023 to present";

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));
        svc.Setup(s => s.GetExtractedTextAsync(id)).ReturnsAsync(rawText);

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("medium", "How much experience do I have?"));

        // Raw dates must appear — not just the summary
        Assert.Contains("Jan 2020", result!.Summary);
        Assert.Contains("extracted text (detailed)", result.Summary);

        // Summary should also appear as a context note
        Assert.Contains("AI summary for context", result.Summary);

        // Anti-fabrication note must be present
        Assert.Contains("Do not invent", result.Summary);

        svc.Verify(s => s.GetExtractedTextAsync(id), Times.Once);
    }

    [Fact]
    public async Task Medium_HasSummary_DetailQuestion_NoExtractedText_FallsBackToSummary()
    {
        // Edge case: doc has summary but IsTextExtracted=false (e.g. summary was set manually)
        var id  = Guid.NewGuid();
        var doc = Doc(id, "Resume", isTextExtracted: false,
                       aiSummary: "Summary without raw text.");

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("medium", "How many years of experience?"));

        Assert.Contains("Summary without raw text", result!.Summary);
        svc.Verify(s => s.GetExtractedTextAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ── 4. high depth → ExtractedText (full) + summary note ──────────────────

    [Fact]
    public async Task High_InjectsFullExtractedText()
    {
        var id  = Guid.NewGuid();
        var doc = Doc(id, "BigDoc", isTextExtracted: true, aiSummary: null);
        const string text = "Full document text that should appear verbatim at high depth.";

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));
        svc.Setup(s => s.GetExtractedTextAsync(id)).ReturnsAsync(text);

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("high"));

        Assert.Contains(text, result!.Summary);
        Assert.Contains("extracted text", result.Summary);
    }

    [Fact]
    public async Task High_HasSummary_AppendsSummaryNoteAlongsideText()
    {
        var id  = Guid.NewGuid();
        var doc = Doc(id, "Resume", isTextExtracted: true,
                       aiSummary: "Concise AI summary.");

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));
        svc.Setup(s => s.GetExtractedTextAsync(id)).ReturnsAsync("Raw employment history text.");

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("high"));

        Assert.Contains("Raw employment history text.", result!.Summary);
        Assert.Contains("Concise AI summary.", result.Summary);
    }

    [Fact]
    public async Task High_TruncatesAt1500Chars()
    {
        var id  = Guid.NewGuid();
        var doc = Doc(id, "HugeDoc", isTextExtracted: true);
        var bigText = new string('X', 3000);

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));
        svc.Setup(s => s.GetExtractedTextAsync(id)).ReturnsAsync(bigText);

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("high"));

        Assert.Contains("…", result!.Summary);
        Assert.DoesNotContain(bigText, result.Summary);
    }

    // ── 5. low depth → stats only ────────────────────────────────────────────

    [Fact]
    public async Task Low_NeverInjectsContent()
    {
        var id  = Guid.NewGuid();
        var doc = Doc(id, "ADoc", isTextExtracted: true, aiSummary: "some summary");

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("low"));

        Assert.Contains("Total documents", result!.Summary);
        Assert.DoesNotContain("some summary", result.Summary);
        Assert.DoesNotContain("extracted text", result.Summary);
        svc.Verify(s => s.GetExtractedTextAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Low_DetailQuestion_StillNoContent()
    {
        var id  = Guid.NewGuid();
        var doc = Doc(id, "Resume", isTextExtracted: true);

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("low", "How much experience do I have?"));

        Assert.DoesNotContain("extracted text", result!.Summary);
        svc.Verify(s => s.GetExtractedTextAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ── 6. CanHandle keyword routing ─────────────────────────────────────────

    [Theory]
    [InlineData("How much experience do I have?",                     true)]  // "experience"
    [InlineData("What is my name according to my uploaded document?", true)]  // "according to" + "document"
    [InlineData("What does the file say?",                            true)]  // "what does" + "file"
    [InlineData("Read my resume please",                              true)]  // "resume" + "read"
    [InlineData("Summarize my report",                                true)]  // "summarize" + "report"
    [InlineData("What is the content of my certificate?",            true)]  // "content" + "certificate"
    [InlineData("Extract text from the file",                         true)]  // "extract" + "file"
    [InlineData("What's in my vault?",                                true)]  // "what's in" + "vault"
    [InlineData("Show my documents",                                  true)]  // "documents"
    [InlineData("What's the weather today?",                          false)] // no match
    [InlineData("Add a note about meetings",                          false)] // "about" not in keywords
    [InlineData("How are you?",                                       false)] // no match
    public void CanHandle_MatchesExpected(string prompt, bool expected)
    {
        var svc = new Mock<IDocumentService>();
        Assert.Equal(expected, new DocumentsWorkspaceTool(svc.Object).CanHandle(prompt));
    }

    // ── 7. IsDetailQuestion detection ────────────────────────────────────────

    [Theory]
    // Affirmative cases
    [InlineData("How much experience do I have?",               true)]
    [InlineData("How many years have I worked?",                true)]
    [InlineData("How long did I work at Accenture?",            true)]
    [InlineData("What is my salary according to the document?", true)]
    [InlineData("When did I join the company?",                 true)]
    [InlineData("Since when am I working?",                     true)]
    [InlineData("What is my qualification?",                    true)]
    [InlineData("What degree do I have?",                       true)]
    [InlineData("Calculate my total experience",                true)]
    [InlineData("What date did I start at Accenture?",          true)]
    [InlineData("List my employers",                            true)]
    [InlineData("Duration of employment at current company",    true)]
    // Negative cases — general questions
    [InlineData("Tell me about my resume",                      false)]
    [InlineData("What is in my document?",                      false)]
    [InlineData("What's the weather today?",                    false)]
    [InlineData("Summarize my resume",                          false)]
    [InlineData("",                                             false)]
    public void IsDetailQuestion_DetectsCorrectly(string prompt, bool expected)
    {
        Assert.Equal(expected, DocumentsWorkspaceTool.IsDetailQuestion(prompt));
    }

    // ── 8. Anti-fabrication note ──────────────────────────────────────────────

    [Fact]
    public async Task Medium_DetailQuestion_IncludesAntiFabricationNote()
    {
        var id  = Guid.NewGuid();
        var doc = Doc(id, "Resume", isTextExtracted: true);
        const string rawText = "Worked at ACME Corp from Jan 2020 to Dec 2022.";

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));
        svc.Setup(s => s.GetExtractedTextAsync(id)).ReturnsAsync(rawText);

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("medium", "How much experience do I have?"));

        Assert.Contains("Do not invent", result!.Summary);
        Assert.Contains("state the assumption explicitly", result.Summary);
    }

    [Fact]
    public async Task Medium_GeneralQuestion_NoAntiFabricationNote()
    {
        var id  = Guid.NewGuid();
        var doc = Doc(id, "Resume", isTextExtracted: true,
                       aiSummary: "Developer with 3 years experience.");

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));

        var result = await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("medium", "Tell me about my resume"));

        Assert.DoesNotContain("Do not invent", result!.Summary);
    }

    // ── 9. Stats present at every depth ──────────────────────────────────────

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public async Task AllDepths_AlwaysIncludeStats(string depth)
    {
        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync())
           .ReturnsAsync(MakeSummary(Array.Empty<DocumentResponse>()));

        var result = await new DocumentsWorkspaceTool(svc.Object).ExecuteAsync(Ctx(depth));

        Assert.NotNull(result);
        Assert.Contains("Total documents", result!.Summary);
        Assert.Contains("Storage used", result.Summary);
    }

    // ── 10. No-text document never triggers GetExtractedTextAsync ────────────

    [Fact]
    public async Task Medium_DocWithNoExtractedText_NeverCallsGetExtractedText()
    {
        var doc = Doc(Guid.NewGuid(), "ImageDoc", isTextExtracted: false);

        var svc = new Mock<IDocumentService>();
        svc.Setup(s => s.GetSummaryAsync()).ReturnsAsync(MakeSummary(new[] { doc }));

        await new DocumentsWorkspaceTool(svc.Object)
            .ExecuteAsync(Ctx("medium", "How much experience do I have?"));

        svc.Verify(s => s.GetExtractedTextAsync(It.IsAny<Guid>()), Times.Never);
    }
}
