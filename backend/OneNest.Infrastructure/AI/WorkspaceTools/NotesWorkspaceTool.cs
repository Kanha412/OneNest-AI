using OneNest.Application.DTOs.AI;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Services;

namespace OneNest.Infrastructure.AI.WorkspaceTools;

public class NotesWorkspaceTool : IAIWorkspaceTool
{
    private static readonly string[] Keywords =
    [
        "note", "notes", "summarize my notes", "summary of notes", "remember"
    ];

    private readonly INoteService _noteService;

    public NotesWorkspaceTool(INoteService noteService)
    {
        _noteService = noteService;
    }

    public string Name => "notes";
    public string Description => "Use for note counts, note summaries, pinned notes, and recent note context.";

    public bool CanHandle(string prompt)
    {
        var text = prompt.ToLowerInvariant();
        return Keywords.Any(text.Contains);
    }

    public async Task<WorkspaceToolResult?> ExecuteAsync(WorkspaceToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        var notes = await _noteService.GetAllAsync();

        var recent = notes
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(5)
            .ToList();

        var lines = new List<string>
        {
            $"Total notes: {notes.Count}",
            $"Pinned notes: {notes.Count(x => x.IsPinned)}"
        };

        if (recent.Count > 0)
        {
            lines.Add("Recent notes:");
            lines.AddRange(recent.Select(x =>
                $"- {x.Title}: {Trim(x.Content, 120)}"));
        }

        return new WorkspaceToolResult
        {
            ToolName = Name,
            Summary = string.Join("\n", lines)
        };
    }

    private static string Trim(string value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length <= max) return text;
        return text[..max] + "...";
    }
}
