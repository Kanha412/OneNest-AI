using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneNest.Application.DTOs.Settings;
using OneNest.Application.Interfaces.Services;

namespace OneNest.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<ActionResult<SettingsResponse>> GetCurrent()
    {
        var settings = await _settingsService.GetCurrentAsync();
        return Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult<SettingsResponse>> Update(UpdateSettingsRequest request)
    {
        try
        {
            var settings = await _settingsService.UpdateAsync(request);
            return Ok(settings);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
