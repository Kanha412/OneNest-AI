using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Expenses;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ExpenseResponse>>> GetAll()
    {
        var expenses = await _expenseService.GetAllAsync();
        return Ok(expenses);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ExpenseSummaryResponse>> GetSummary()
    {
        var summary = await _expenseService.GetSummaryAsync();
        return Ok(summary);
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> Create(CreateExpenseRequest request)
    {
        var expense = await _expenseService.CreateAsync(request);
        return Ok(expense);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateExpenseRequest request)
    {
        var expense = await _expenseService.UpdateAsync(id, request);

        if (expense is null)
            return NotFound();

        return Ok(expense);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _expenseService.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
