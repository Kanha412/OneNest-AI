using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Notes;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotesController : ControllerBase
{
    private readonly INoteService _noteService;

    public NotesController(INoteService noteService)
    {
        _noteService = noteService;
    }

    [HttpGet]
    public async Task<ActionResult<List<NoteResponse>>> GetAll()
    {
        var notes = await _noteService.GetAllAsync();
        return Ok(notes);
    }

    [HttpPost]
    public async Task<ActionResult<NoteResponse>> Create(CreateNoteRequest request)
    {
        var note = await _noteService.CreateAsync(request);
        return Ok(note);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _noteService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPut("{id}")]
public async Task<IActionResult> Update(Guid id, UpdateNoteRequest request)
{
    var note = await _noteService.UpdateAsync(id, request);

    if (note == null)
        return NotFound();

    return Ok(note);
}

[HttpPatch("{id}/pin")]
public async Task<IActionResult> TogglePin(Guid id)
{
    await _noteService.TogglePinAsync(id);

    return NoContent();
}
}