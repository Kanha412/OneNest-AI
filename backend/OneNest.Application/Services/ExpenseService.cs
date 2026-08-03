using OneNest.Application.DTOs.Expenses;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;
using OneNest.Domain.Enums;

namespace OneNest.Application.Services;

public class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICurrentUserService _currentUserService;

    public ExpenseService(IExpenseRepository expenseRepository, ICurrentUserService currentUserService)
    {
        _expenseRepository = expenseRepository;
        _currentUserService = currentUserService;
    }

    public async Task<List<ExpenseResponse>> GetAllAsync()
    {
        var expenses = await _expenseRepository.GetAllAsync(_currentUserService.UserId);

        return expenses.Select(MapToResponse).ToList();
    }

    public async Task<ExpenseResponse> CreateAsync(CreateExpenseRequest request)
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Amount = request.Amount,
            Category = request.Category,
            TransactionType = request.TransactionType,
            Date = request.Date,
            Notes = request.Notes,
            UserId = _currentUserService.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _expenseRepository.AddAsync(expense);

        return MapToResponse(expense);
    }

    public async Task<ExpenseResponse?> UpdateAsync(Guid id, UpdateExpenseRequest request)
    {
        var expense = await _expenseRepository.GetByIdAsync(id, _currentUserService.UserId);

        if (expense is null)
            return null;

        expense.Title = request.Title;
        expense.Amount = request.Amount;
        expense.Category = request.Category;
        expense.TransactionType = request.TransactionType;
        expense.Date = request.Date;
        expense.Notes = request.Notes;
        expense.UpdatedAt = DateTime.UtcNow;

        await _expenseRepository.UpdateAsync(expense);

        return MapToResponse(expense);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var expense = await _expenseRepository.GetByIdAsync(id, _currentUserService.UserId);

        if (expense is null)
            return false;

        await _expenseRepository.DeleteAsync(expense);
        return true;
    }

    public async Task<ExpenseSummaryResponse> GetSummaryAsync()
    {
        var expenses = await _expenseRepository.GetAllAsync(_currentUserService.UserId);

        var income = expenses.Where(x => x.TransactionType == TransactionType.Income).ToList();
        var expense = expenses.Where(x => x.TransactionType == TransactionType.Expense).ToList();

        var totalIncome = income.Sum(x => x.Amount);
        var totalExpense = expense.Sum(x => x.Amount);

        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        var thisMonthIncome = income
            .Where(x => x.Date.Year == now.Year && x.Date.Month == now.Month)
            .Sum(x => x.Amount);

        var thisMonthExpense = expense
            .Where(x => x.Date.Year == now.Year && x.Date.Month == now.Month)
            .Sum(x => x.Amount);

        var categoryBreakdown = expense
            .GroupBy(x => x.Category)
            .Select(g => new CategoryExpenseResponse
            {
                Category = g.Key,
                TotalAmount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        var monthlyBreakdown = expenses
            .GroupBy(x => new { x.Date.Year, x.Date.Month })
            .Select(g => new MonthlyExpenseResponse
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Income = g.Where(x => x.TransactionType == TransactionType.Income).Sum(x => x.Amount),
                Expense = g.Where(x => x.TransactionType == TransactionType.Expense).Sum(x => x.Amount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToList();

        return new ExpenseSummaryResponse
        {
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            CurrentBalance = totalIncome - totalExpense,
            ThisMonthIncome = thisMonthIncome,
            ThisMonthExpense = thisMonthExpense,
            TopExpenseCategory = categoryBreakdown.Count > 0 ? categoryBreakdown[0].Category : null,
            RecentTransactions = expenses
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(MapToResponse)
                .ToList(),
            CategoryBreakdown = categoryBreakdown,
            MonthlyBreakdown = monthlyBreakdown
        };
    }

    private static ExpenseResponse MapToResponse(Expense expense)
    {
        return new ExpenseResponse
        {
            Id = expense.Id,
            Title = expense.Title,
            Amount = expense.Amount,
            Category = expense.Category,
            TransactionType = expense.TransactionType,
            Date = expense.Date,
            Notes = expense.Notes,
            CreatedAt = expense.CreatedAt,
            UpdatedAt = expense.UpdatedAt
        };
    }
}
