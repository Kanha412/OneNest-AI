using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Health;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MedicalRecordsController : ControllerBase
{
    private readonly IMedicalRecordService _medicalRecordService;

    public MedicalRecordsController(IMedicalRecordService medicalRecordService)
    {
        _medicalRecordService = medicalRecordService;
    }

    [HttpGet]
    public async Task<ActionResult<MedicalRecordResponse>> Get()
    {
        var record = await _medicalRecordService.GetAsync();

        if (record is null)
            return NoContent();

        return Ok(record);
    }

    [HttpPut]
    public async Task<ActionResult<MedicalRecordResponse>> Save(SaveMedicalRecordRequest request)
    {
        var record = await _medicalRecordService.SaveAsync(request);
        return Ok(record);
    }
}
