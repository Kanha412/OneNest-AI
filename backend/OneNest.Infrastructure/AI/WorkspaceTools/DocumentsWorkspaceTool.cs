using OneNest.Application.DTOs.AI;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Services;

namespace OneNest.Infrastructure.AI.WorkspaceTools;

public class DocumentsWorkspaceTool : IAIWorkspaceTool
{
    private static readonly string[] Keywords =
    [
        "document", "documents", "file", "files", "vault", "upload", "pdf", "report"
    ];

    private readonly IDocumentService _documentService;

    public DocumentsWorkspaceTool(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public string Name => "documents";
    public string Description => "Use for document vault summaries, uploads, file counts, and recent documents.";

    public bool CanHandle(string prompt)
    {
        var text = prompt.ToLowerInvariant();
        return Keywords.Any(text.Contains);
    }

    public async Task<WorkspaceToolResult?> ExecuteAsync(WorkspaceToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        var summary = await _documentService.GetSummaryAsync();

        var lines = new List<string>
        {
            $"Total documents: {summary.TotalDocuments}",
            $"Today's uploads: {summary.TodayUploads}",
            $"Storage used (bytes): {summary.StorageUsed}"
        };

        if (summary.RecentDocuments.Count > 0)
        {
            lines.Add("Recent documents:");
            lines.AddRange(summary.RecentDocuments.Select(x =>
                $"- {x.Title} ({x.OriginalFileName}) [{x.Category}]"));
        }

        return new WorkspaceToolResult
        {
            ToolName = Name,
            Summary = string.Join("\n", lines)
        };
    }
}
