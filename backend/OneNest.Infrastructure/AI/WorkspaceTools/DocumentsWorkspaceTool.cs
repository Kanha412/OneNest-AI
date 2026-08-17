using OneNest.Application.DTOs.AI;
using OneNest.Application.DTOs.Documents;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Services;

namespace OneNest.Infrastructure.AI.WorkspaceTools;

/// <summary>
/// Provides document vault context to the AI assistant.
///
/// Content depth matrix:
///
///   low    → stats + file list only; no document text is ever injected.
///
///   medium → stats + file list + per-document content:
///              • General question   → AISummary (if exists) ELSE ExtractedText ≤500 chars
///              • Detail question*   → ExtractedText ≤1000 chars PLUS AISummary note (if exists)
///
///   high   → stats + file list + ExtractedText ≤1500 chars + AISummary note (if exists)
///
/// * A "detail question" is one that asks for specific facts — dates, durations, years of
///   experience, employment history, qualifications, numbers, salaries, etc.  When the AI
///   only receives a generated summary it cannot answer those accurately; the raw
///   extracted text is therefore always preferred for such prompts.
/// </summary>
public class DocumentsWorkspaceTool : IAIWorkspaceTool
{
    // ── Keyword sets ────────────────────────────────────────────────────────

    /// <summary>Route keywords — any match causes this tool to be called.</summary>
    private static readonly string[] RouteKeywords =
    [
        "document", "documents", "file", "files", "vault", "upload", "pdf", "report",
        "read", "content", "inside", "extract", "summarize", "summary",
        "resume", "cv", "certificate", "qualification",
        "experience", "employment", "education",
        "according to", "what does", "what's in"
    ];

    /// <summary>
    /// Detail-seeking keywords — presence means the user needs specific facts
    /// (dates, durations, numbers, etc.) and the AI summary may not be sufficient.
    /// </summary>
    private static readonly string[] DetailKeywords =
    [
        "how much", "how many", "how long",
        "experience", "years", "months",
        "date", "since", "when",
        "qualification", "degree",
        "salary", "amount", "total",
        "duration", "calculate", "period",
        "company", "employer", "employment", "worked",
        "joined", "promoted", "hired", "left"
    ];

    // ── Character limits ────────────────────────────────────────────────────

    private const int TopDocumentCount       = 3;
    private const int MediumSummaryChars     = 500;   // AISummary at medium (general)
    private const int MediumTextChars        = 500;   // ExtractedText at medium (no summary)
    private const int MediumDetailTextChars  = 1_000; // ExtractedText at medium (detail question)
    private const int HighTextChars          = 1_500; // ExtractedText at high depth
    private const int SummaryNoteChars       = 200;   // AISummary appended as a note alongside text

    private readonly IDocumentService _documentService;

    public DocumentsWorkspaceTool(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public string Name => "documents";
    public string Description =>
        "Use for document vault summaries, uploads, file counts, recent documents, and reading document content.";

    // ── IAIWorkspaceTool ────────────────────────────────────────────────────

    public bool CanHandle(string prompt)
    {
        var lower = prompt.ToLowerInvariant();
        return RouteKeywords.Any(lower.Contains);
    }

    public async Task<WorkspaceToolResult?> ExecuteAsync(
        WorkspaceToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var summary    = await _documentService.GetSummaryAsync();
        var depth      = NormalizeDepth(context.ContextDepth);
        var needDetail = IsDetailQuestion(context.UserPrompt);

        // ── Always: stats + file list ──────────────────────────────────────
        var lines = new List<string>
        {
            $"Total documents: {summary.TotalDocuments}",
            $"Today's uploads: {summary.TodayUploads}",
            $"Storage used (bytes): {summary.StorageUsed}"
        };

        if (summary.RecentDocuments.Count > 0)
        {
            lines.Add("Recent documents:");
            foreach (var doc in summary.RecentDocuments)
            {
                var flags = new List<string>();
                if (doc.IsTextExtracted)    flags.Add("text extracted");
                if (doc.AISummary is not null) flags.Add("AI summary");
                var flagStr = flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : string.Empty;
                lines.Add($"- {doc.Title} ({doc.OriginalFileName}) [{doc.Category}]{flagStr}");
            }
        }

        // ── medium / high: inject document content ─────────────────────────
        if (depth == "medium" || depth == "high")
        {
            var docsWithContent = summary.RecentDocuments
                .Where(x => x.IsTextExtracted || x.AISummary is not null)
                .Take(TopDocumentCount)
                .ToList();

            if (docsWithContent.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add(depth == "high"
                    ? "Document contents (full text):"
                    : needDetail
                        ? "Document contents (detailed view — specific facts requested):"
                        : "Document contents:");

                foreach (var doc in docsWithContent)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (depth == "high")
                    {
                        await AppendHighContent(lines, doc, cancellationToken);
                    }
                    else
                    {
                        await AppendMediumContent(lines, doc, needDetail, cancellationToken);
                    }
                }

                // Append a hint so the model knows not to fabricate missing info
                if (needDetail)
                {
                    lines.Add(string.Empty);
                    lines.Add(
                        "Note: answer only from the document text above. " +
                        "If dates are partial or missing, state the assumption explicitly. " +
                        "Do not invent experience, dates, or figures not present in the text.");
                }
            }
        }

        return new WorkspaceToolResult
        {
            ToolName = Name,
            Summary  = string.Join("\n", lines)
        };
    }

    // ── Content injection helpers ───────────────────────────────────────────

    /// <summary>
    /// Medium depth:
    ///   • General question   → prefer AISummary; fall back to short ExtractedText.
    ///   • Detail question    → always use ExtractedText (longer slice); append
    ///                          AISummary as a brief orientation note if it exists.
    /// </summary>
    private async Task AppendMediumContent(
        List<string> lines,
        DocumentResponse doc,
        bool needDetail,
        CancellationToken cancellationToken)
    {
        if (!needDetail && !string.IsNullOrWhiteSpace(doc.AISummary))
        {
            // General question + summary exists → lightweight path
            lines.Add($"[{doc.Title}] (AI summary): {Truncate(doc.AISummary, MediumSummaryChars)}");
            return;
        }

        if (doc.IsTextExtracted)
        {
            var rawText = await _documentService.GetExtractedTextAsync(doc.Id);
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                var limit  = needDetail ? MediumDetailTextChars : MediumTextChars;
                var label  = needDetail ? "extracted text (detailed)" : "extracted text preview";
                lines.Add($"[{doc.Title}] ({label}): {Truncate(rawText, limit)}");

                // For detail questions: also append the AI summary as orientation context
                // so the model can cross-reference the prose summary with the raw dates.
                if (needDetail && !string.IsNullOrWhiteSpace(doc.AISummary))
                {
                    lines.Add($"[{doc.Title}] (AI summary for context): {Truncate(doc.AISummary, SummaryNoteChars)}");
                }

                return;
            }
        }

        // Last resort: extracted text unavailable but summary exists
        if (!string.IsNullOrWhiteSpace(doc.AISummary))
        {
            lines.Add($"[{doc.Title}] (AI summary): {Truncate(doc.AISummary, MediumSummaryChars)}");
        }
    }

    /// <summary>
    /// High depth: always injects ExtractedText (up to 1500 chars).
    /// Appends AISummary as a supplementary note when present.
    /// </summary>
    private async Task AppendHighContent(
        List<string> lines,
        DocumentResponse doc,
        CancellationToken cancellationToken)
    {
        if (doc.IsTextExtracted)
        {
            var rawText = await _documentService.GetExtractedTextAsync(doc.Id);
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                lines.Add($"[{doc.Title}] (extracted text):");
                lines.Add(Truncate(rawText, HighTextChars));
            }
        }

        if (!string.IsNullOrWhiteSpace(doc.AISummary))
        {
            lines.Add($"[{doc.Title}] (AI summary): {Truncate(doc.AISummary, SummaryNoteChars)}");
        }
    }

    // ── Static helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the prompt is asking for specific factual details
    /// (dates, durations, experience, numbers) that a prose summary may omit.
    /// </summary>
    public static bool IsDetailQuestion(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return false;
        var lower = prompt.ToLowerInvariant();
        return DetailKeywords.Any(lower.Contains);
    }

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            return text;
        return text[..maxChars] + "…";
    }

    private static string NormalizeDepth(string? depth)
    {
        var normalized = depth?.Trim().ToLowerInvariant();
        return normalized is "low" or "medium" or "high" ? normalized : "medium";
    }
}
