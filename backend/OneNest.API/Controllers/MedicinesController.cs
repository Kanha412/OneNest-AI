using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Health;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MedicinesController : ControllerBase
{
    private readonly IMedicineService _medicineService;

    public MedicinesController(IMedicineService medicineService)
    {
        _medicineService = medicineService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MedicineResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool? isActive)
    {
        var medicines = await _medicineService.GetAllAsync(search, isActive);
        return Ok(medicines);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MedicineResponse>> GetById(Guid id)
    {
        var medicine = await _medicineService.GetByIdAsync(id);

        if (medicine is null)
            return NotFound();

        return Ok(medicine);
    }

    [HttpPost]
    public async Task<ActionResult<MedicineResponse>> Create(CreateMedicineRequest request)
    {
        try
        {
            var medicine = await _medicineService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = medicine.Id }, medicine);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateMedicineRequest request)
    {
        try
        {
            var medicine = await _medicineService.UpdateAsync(id, request);

            if (medicine is null)
                return NotFound();

            return Ok(medicine);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _medicineService.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
