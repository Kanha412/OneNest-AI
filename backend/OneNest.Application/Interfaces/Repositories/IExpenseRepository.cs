using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface IExpenseRepository
{
    Task<List<Expense>> GetAllAsync(Guid userId);

    Task<Expense?> GetByIdAsync(Guid id, Guid userId);

    Task AddAsync(Expense expense);

    Task UpdateAsync(Expense expense);

    Task DeleteAsync(Expense expense);
}
