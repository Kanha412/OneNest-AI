using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Documents;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Enums;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] DocumentCategory? category)
    {
        var documents = await _documentService.GetAllAsync(search, category);
        return Ok(documents);
    }

    [HttpGet("recent")]
    public async Task<ActionResult<List<DocumentResponse>>> GetRecent([FromQuery] int count = 5)
    {
        var documents = await _documentService.GetRecentAsync(count);
        return Ok(documents);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DocumentSummaryResponse>> GetSummary()
    {
        var summary = await _documentService.GetSummaryAsync();
        return Ok(summary);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentResponse>> GetById(Guid id)
    {
        var document = await _documentService.GetByIdAsync(id);

        if (document is null)
            return NotFound();

        return Ok(document);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var file = await _documentService.DownloadAsync(id);

        if (file is null)
            return NotFound();

        return File(file.Content, file.ContentType, file.OriginalFileName);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id)
    {
        var file = await _documentService.DownloadAsync(id);

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
    public async Task<ActionResult<DocumentResponse>> Upload(
        IFormFile file,
        [FromForm] string title,
        [FromForm] DocumentCategory category = DocumentCategory.Other,
        [FromForm] string? description = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A file is required.");

        await using var stream = file.OpenReadStream();

        var input = new UploadDocumentInput
        {
            Metadata = new CreateDocumentRequest
            {
                Title = title,
                Category = category,
                Description = description ?? string.Empty
            },
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            Content = stream
        };

        try
        {
            var document = await _documentService.UploadAsync(input);
            return Ok(document);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateDocumentRequest request)
    {
        var document = await _documentService.UpdateAsync(id, request);

        if (document is null)
            return NotFound();

        return Ok(document);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _documentService.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
