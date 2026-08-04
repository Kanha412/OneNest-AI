using OneNest.Application.DTOs.AI;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Services;

namespace OneNest.Infrastructure.AI.WorkspaceTools;

public class HealthWorkspaceTool : IAIWorkspaceTool
{
    private static readonly string[] Keywords =
    [
        "health", "medicine", "medicines", "appointment", "appointments", "medical", "report", "doctor", "hospital"
    ];

    private readonly IHealthSummaryService _healthSummaryService;

    public HealthWorkspaceTool(IHealthSummaryService healthSummaryService)
    {
        _healthSummaryService = healthSummaryService;
    }

    public string Name => "health";
    public string Description => "Use for medicines, appointments, medical reports, and health summary questions.";

    public bool CanHandle(string prompt)
    {
        var text = prompt.ToLowerInvariant();
        return Keywords.Any(text.Contains);
    }

    public async Task<WorkspaceToolResult?> ExecuteAsync(WorkspaceToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        var summary = await _healthSummaryService.GetSummaryAsync();

        var lines = new List<string>
        {
            $"Active medicines: {summary.ActiveMedicines}",
            $"Today's medicines: {summary.TodayMedicines}",
            $"Upcoming appointments: {summary.UpcomingAppointments}",
            $"Total medical reports: {summary.TotalReports}"
        };

        if (summary.UpcomingAppointmentsList.Count > 0)
        {
            lines.Add("Upcoming appointments list:");
            lines.AddRange(summary.UpcomingAppointmentsList.Select(x =>
                $"- {x.Date:yyyy-MM-dd} {x.Time:HH\\:mm} with Dr. {x.DoctorName} ({x.Specialty}) at {x.Hospital}"));
        }

        if (summary.RecentReports.Count > 0)
        {
            lines.Add("Recent medical reports:");
            lines.AddRange(summary.RecentReports.Select(x =>
                $"- {x.Title} ({x.Category}) on {x.ReportDate:yyyy-MM-dd}"));
        }

        return new WorkspaceToolResult
        {
            ToolName = Name,
            Summary = string.Join("\n", lines)
        };
    }
}
