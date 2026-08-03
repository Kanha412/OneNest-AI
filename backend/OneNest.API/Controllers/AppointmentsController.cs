using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Health;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Enums;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AppointmentResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] AppointmentStatus? status)
    {
        var appointments = await _appointmentService.GetAllAsync(search, status);
        return Ok(appointments);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AppointmentResponse>> GetById(Guid id)
    {
        var appointment = await _appointmentService.GetByIdAsync(id);

        if (appointment is null)
            return NotFound();

        return Ok(appointment);
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentResponse>> Create(CreateAppointmentRequest request)
    {
        var appointment = await _appointmentService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateAppointmentRequest request)
    {
        var appointment = await _appointmentService.UpdateAsync(id, request);

        if (appointment is null)
            return NotFound();

        return Ok(appointment);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _appointmentService.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
