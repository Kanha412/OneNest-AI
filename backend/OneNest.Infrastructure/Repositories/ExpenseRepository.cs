using Microsoft.EntityFrameworkCore;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Domain.Entities;
using OneNest.Infrastructure.Data;

namespace OneNest.Infrastructure.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly OneNestDbContext _dbContext;

    public ExpenseRepository(OneNestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Expense>> GetAllAsync(Guid userId)
    {
        return await _dbContext.Expenses
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Expense?> GetByIdAsync(Guid id, Guid userId)
    {
        return await _dbContext.Expenses
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    public async Task AddAsync(Expense expense)
    {
        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Expense expense)
    {
        _dbContext.Expenses.Update(expense);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Expense expense)
    {
        _dbContext.Expenses.Remove(expense);
        await _dbContext.SaveChangesAsync();
    }
}
