using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Expenses;

public class ExpenseResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public ExpenseCategory Category { get; set; }

    public TransactionType TransactionType { get; set; }

    public DateOnly Date { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
