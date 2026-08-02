using Microsoft.AspNetCore.Mvc;

namespace OneNest.API.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            application = "OneNest AI",
            version = "1.0.0",
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
