using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Tasks;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskResponse>>> GetAll()
    {
        var tasks = await _taskService.GetAllAsync();
        return Ok(tasks);
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create(CreateTaskRequest request)
    {
        var task = await _taskService.CreateAsync(request);
        return Ok(task);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTaskRequest request)
    {
        var task = await _taskService.UpdateAsync(id, request);

        if (task is null)
            return NotFound();

        return Ok(task);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _taskService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPatch("{id:guid}/complete")]
    public async Task<IActionResult> ToggleComplete(Guid id)
    {
        await _taskService.ToggleCompleteAsync(id);
        return NoContent();
    }
}