using OneNest.Domain.Enums;

namespace OneNest.Domain.Entities;

public class Expense
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;

    public TransactionType TransactionType { get; set; } = TransactionType.Expense;

    public DateOnly Date { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
