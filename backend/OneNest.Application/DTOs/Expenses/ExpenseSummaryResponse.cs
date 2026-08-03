using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Expenses;

public class CategoryExpenseResponse
{
    public ExpenseCategory Category { get; set; }

    public decimal TotalAmount { get; set; }
}

public class MonthlyExpenseResponse
{
    public int Year { get; set; }

    public int Month { get; set; }

    public decimal Income { get; set; }

    public decimal Expense { get; set; }
}

public class ExpenseSummaryResponse
{
    public decimal TotalIncome { get; set; }

    public decimal TotalExpense { get; set; }

    public decimal CurrentBalance { get; set; }

    public decimal ThisMonthIncome { get; set; }

    public decimal ThisMonthExpense { get; set; }

    public ExpenseCategory? TopExpenseCategory { get; set; }

    public List<ExpenseResponse> RecentTransactions { get; set; } = new();

    public List<CategoryExpenseResponse> CategoryBreakdown { get; set; } = new();

    public List<MonthlyExpenseResponse> MonthlyBreakdown { get; set; } = new();
}
