using System.ComponentModel.DataAnnotations;
using OneNest.Domain.Enums;

namespace OneNest.Application.DTOs.Expenses;

public class CreateExpenseRequest
{
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;

    public TransactionType TransactionType { get; set; } = TransactionType.Expense;

    [Required]
    public DateOnly Date { get; set; }

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;
}
