namespace OneNest.Application.DTOs.Settings;

public class SettingsResponse
{
    public AccountSettingsResponse Account { get; set; } = new();

    public AiPreferencesSettingsResponse AiPreferences { get; set; } = new();

    public NotificationSettingsResponse Notifications { get; set; } = new();

    public DocumentSettingsResponse Documents { get; set; } = new();

    public HealthSettingsResponse Health { get; set; } = new();

    public AppearanceSettingsResponse Appearance { get; set; } = new();

    public PrivacySettingsResponse Privacy { get; set; } = new();

    public AboutSettingsResponse About { get; set; } = new();
}

public class AccountSettingsResponse
{
    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime MemberSince { get; set; }

    public DateTime? LastLoginAt { get; set; }
}

public class AiPreferencesSettingsResponse
{
    public bool EnableWorkspaceContext { get; set; }

    public string ContextDepth { get; set; } = "medium";

    public string DefaultConversationMode { get; set; } = "workspace";

    public string ResponseStyle { get; set; } = "balanced";

    public bool EnableSmartSuggestions { get; set; } = true;
}

public class NotificationSettingsResponse
{
    public bool EnableAppointmentReminders { get; set; } = true;

    public bool EnableMedicineReminders { get; set; } = true;

    public bool EnableTaskReminders { get; set; } = true;

    public bool EnableWeeklySummary { get; set; } = true;

    public bool EnableDesktopNotifications { get; set; }
}

public class DocumentSettingsResponse
{
    public int AutoDeleteTrashDays { get; set; } = 30;
}

public class HealthSettingsResponse
{
    public string DefaultHeightUnit { get; set; } = "cm";

    public string DefaultWeightUnit { get; set; } = "kg";

    public int ReminderLeadTimeHours { get; set; } = 24;
}

public class AppearanceSettingsResponse
{
    public string Theme { get; set; } = "system";

    public bool CompactSidebar { get; set; }

    public bool EnableAnimations { get; set; } = true;
}

public class PrivacySettingsResponse
{
    public bool CanChangePassword { get; set; } = true;

    public bool CanExportData { get; set; } = true;

    public bool CanDeleteAccount { get; set; } = true;

    public bool HasLoggedInDevices { get; set; }
}

public class AboutSettingsResponse
{
    public string ApplicationVersion { get; set; } = "1.0.0";

    public string BuildVersion { get; set; } = "2026.08";

    public string Developer { get; set; } = "OneNest AI Team";

    public string Copyright { get; set; } = "© OneNest AI";
}
