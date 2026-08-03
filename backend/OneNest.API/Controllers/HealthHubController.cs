using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Health;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/health-hub")]
public class HealthHubController : ControllerBase
{
    private readonly IHealthSummaryService _healthSummaryService;

    public HealthHubController(IHealthSummaryService healthSummaryService)
    {
        _healthSummaryService = healthSummaryService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<HealthSummaryResponse>> GetSummary()
    {
        var summary = await _healthSummaryService.GetSummaryAsync();
        return Ok(summary);
    }
}
