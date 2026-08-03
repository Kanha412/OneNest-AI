using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Health;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Enums;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MedicalReportsController : ControllerBase
{
    private readonly IMedicalReportService _medicalReportService;

    public MedicalReportsController(IMedicalReportService medicalReportService)
    {
        _medicalReportService = medicalReportService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MedicalReportResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] MedicalReportCategory? category)
    {
        var reports = await _medicalReportService.GetAllAsync(search, category);
        return Ok(reports);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MedicalReportResponse>> GetById(Guid id)
    {
        var report = await _medicalReportService.GetByIdAsync(id);

        if (report is null)
            return NotFound();

        return Ok(report);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var file = await _medicalReportService.DownloadAsync(id);

        if (file is null)
            return NotFound();

        return File(file.Content, file.ContentType, file.OriginalFileName);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id)
    {
        var file = await _medicalReportService.DownloadAsync(id);

        if (file is null)
            return NotFound();

        Response.Headers.Append(
            "Content-Disposition",
            $"inline; filename=\"{file.OriginalFileName}\"");

        return File(file.Content, file.ContentType);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(26_214_400)] // 25 MB
    public async Task<ActionResult<MedicalReportResponse>> Upload(
        IFormFile file,
        [FromForm] string title,
        [FromForm] MedicalReportCategory category = MedicalReportCategory.Other,
        [FromForm] string? doctorName = null,
        [FromForm] string? hospital = null,
        [FromForm] DateOnly? reportDate = null,
        [FromForm] string? description = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A file is required.");

        await using var stream = file.OpenReadStream();

        var input = new UploadMedicalReportInput
        {
            Metadata = new CreateMedicalReportRequest
            {
                Title = title,
                Category = category,
                DoctorName = doctorName ?? string.Empty,
                Hospital = hospital ?? string.Empty,
                ReportDate = reportDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                Description = description ?? string.Empty
            },
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            Content = stream
        };

        try
        {
            var report = await _medicalReportService.UploadAsync(input);
            return Ok(report);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateMedicalReportRequest request)
    {
        var report = await _medicalReportService.UpdateAsync(id, request);

        if (report is null)
            return NotFound();

        return Ok(report);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _medicalReportService.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
