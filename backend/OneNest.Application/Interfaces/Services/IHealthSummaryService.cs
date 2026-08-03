using OneNest.Application.DTOs.Health;

namespace OneNest.Application.Interfaces.Services;

public interface IHealthSummaryService
{
    Task<HealthSummaryResponse> GetSummaryAsync();
}
