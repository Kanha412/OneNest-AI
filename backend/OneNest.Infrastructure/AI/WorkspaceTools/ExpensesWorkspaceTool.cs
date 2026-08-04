using OneNest.Application.DTOs.AI;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Services;

namespace OneNest.Infrastructure.AI.WorkspaceTools;

public class ExpensesWorkspaceTool : IAIWorkspaceTool
{
    private static readonly string[] Keywords =
    [
        "expense", "expenses", "spend", "spent", "income", "balance", "money", "budget", "food"
    ];

    private readonly IExpenseService _expenseService;

    public ExpensesWorkspaceTool(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public string Name => "expenses";

    public bool CanHandle(string prompt)
    {
        var text = prompt.ToLowerInvariant();
        return Keywords.Any(text.Contains);
    }

    public async Task<WorkspaceToolResult?> ExecuteAsync(WorkspaceToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        var summary = await _expenseService.GetSummaryAsync();
        var topCategory = summary.TopExpenseCategory?.ToString() ?? "N/A";

        var lines = new List<string>
        {
            $"Current balance: {summary.CurrentBalance}",
            $"This month income: {summary.ThisMonthIncome}",
            $"This month expense: {summary.ThisMonthExpense}",
            $"Top expense category: {topCategory}"
        };

        if (summary.RecentTransactions.Count > 0)
        {
            lines.Add("Recent transactions:");
            lines.AddRange(summary.RecentTransactions.Select(x =>
                $"- {x.Title}: {x.Amount} ({x.TransactionType}) on {x.Date:yyyy-MM-dd}"));
        }

        return new WorkspaceToolResult
        {
            ToolName = Name,
            Summary = string.Join("\n", lines)
        };
    }
}
