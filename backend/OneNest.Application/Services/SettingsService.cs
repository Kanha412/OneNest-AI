using OneNest.Application.DTOs.Settings;
using OneNest.Application.Interfaces.Repositories;
using OneNest.Application.Interfaces.Security;
using OneNest.Application.Interfaces.Services;
using OneNest.Domain.Entities;

namespace OneNest.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly IUserSettingsRepository _userSettingsRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public SettingsService(
        IUserSettingsRepository userSettingsRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _userSettingsRepository = userSettingsRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<SettingsResponse> GetCurrentAsync()
    {
        var userId = _currentUserService.UserId;
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var settings = await EnsureSettingsAsync(userId);
        return MapResponse(user, settings);
    }

    public async Task<SettingsResponse> UpdateAsync(UpdateSettingsRequest request)
    {
        var userId = _currentUserService.UserId;
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var displayName = (request.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("Display name is required.");

        var settings = await EnsureSettingsAsync(userId);

        user.FullName = displayName;
        user.UpdatedAt = DateTime.UtcNow;

        settings.EnableWorkspaceContext = request.EnableWorkspaceContext;
        settings.ContextDepth = Normalize(request.ContextDepth, "medium");
        settings.DefaultConversationMode = Normalize(request.DefaultConversationMode, "workspace");
        settings.ResponseStyle = Normalize(request.ResponseStyle, "balanced");
        settings.EnableSmartSuggestions = request.EnableSmartSuggestions;

        settings.EnableAppointmentReminders = request.EnableAppointmentReminders;
        settings.EnableMedicineReminders = request.EnableMedicineReminders;
        settings.EnableTaskReminders = request.EnableTaskReminders;
        settings.EnableWeeklySummary = request.EnableWeeklySummary;
        settings.EnableDesktopNotifications = request.EnableDesktopNotifications;

        settings.AutoDeleteTrashDays = request.AutoDeleteTrashDays;

        settings.DefaultHeightUnit = Normalize(request.DefaultHeightUnit, "cm");
        settings.DefaultWeightUnit = Normalize(request.DefaultWeightUnit, "kg");
        settings.ReminderLeadTimeHours = request.ReminderLeadTimeHours;

        settings.Theme = Normalize(request.Theme, "system");
        settings.CompactSidebar = request.CompactSidebar;
        settings.EnableAnimations = request.EnableAnimations;
        settings.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);
        await _userSettingsRepository.UpdateAsync(settings);

        return MapResponse(user, settings);
    }

    private async Task<UserSettings> EnsureSettingsAsync(Guid userId)
    {
        var settings = await _userSettingsRepository.GetByUserIdAsync(userId);
        if (settings is not null)
            return settings;

        settings = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _userSettingsRepository.AddAsync(settings);
        return settings;
    }

    private static SettingsResponse MapResponse(User user, UserSettings settings)
    {
        return new SettingsResponse
        {
            Account = new AccountSettingsResponse
            {
                DisplayName = user.FullName,
                Email = user.Email,
                MemberSince = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            },
            AiPreferences = new AiPreferencesSettingsResponse
            {
                EnableWorkspaceContext = settings.EnableWorkspaceContext,
                ContextDepth = settings.ContextDepth,
                DefaultConversationMode = settings.DefaultConversationMode,
                ResponseStyle = settings.ResponseStyle,
                EnableSmartSuggestions = settings.EnableSmartSuggestions
            },
            Notifications = new NotificationSettingsResponse
            {
                EnableAppointmentReminders = settings.EnableAppointmentReminders,
                EnableMedicineReminders = settings.EnableMedicineReminders,
                EnableTaskReminders = settings.EnableTaskReminders,
                EnableWeeklySummary = settings.EnableWeeklySummary,
                EnableDesktopNotifications = settings.EnableDesktopNotifications
            },
            Documents = new DocumentSettingsResponse
            {
                AutoDeleteTrashDays = settings.AutoDeleteTrashDays
            },
            Health = new HealthSettingsResponse
            {
                DefaultHeightUnit = settings.DefaultHeightUnit,
                DefaultWeightUnit = settings.DefaultWeightUnit,
                ReminderLeadTimeHours = settings.ReminderLeadTimeHours
            },
            Appearance = new AppearanceSettingsResponse
            {
                Theme = settings.Theme,
                CompactSidebar = settings.CompactSidebar,
                EnableAnimations = settings.EnableAnimations
            },
            Privacy = new PrivacySettingsResponse(),
            About = new AboutSettingsResponse()
        };
    }

    private static string Normalize(string? value, string fallback)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
