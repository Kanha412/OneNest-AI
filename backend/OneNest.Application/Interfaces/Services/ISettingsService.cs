using OneNest.Application.DTOs.Settings;

namespace OneNest.Application.Interfaces.Services;

public interface ISettingsService
{
    Task<SettingsResponse> GetCurrentAsync();

    Task<SettingsResponse> UpdateAsync(UpdateSettingsRequest request);
}
