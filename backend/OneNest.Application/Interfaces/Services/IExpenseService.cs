using OneNest.Application.DTOs.Expenses;

namespace OneNest.Application.Interfaces.Services;

public interface IExpenseService
{
    Task<List<ExpenseResponse>> GetAllAsync();

    Task<ExpenseResponse> CreateAsync(CreateExpenseRequest request);

    Task<ExpenseResponse?> UpdateAsync(Guid id, UpdateExpenseRequest request);

    Task<bool> DeleteAsync(Guid id);

    Task<ExpenseSummaryResponse> GetSummaryAsync();
}
