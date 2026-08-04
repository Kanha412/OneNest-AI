namespace OneNest.Domain.Entities;

public class UserSettings
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Theme { get; set; } = "system";

    public bool CompactSidebar { get; set; }

    public bool EnableAnimations { get; set; } = true;

    public bool EnableWorkspaceContext { get; set; } = true;

    public string ContextDepth { get; set; } = "medium";

    public string DefaultConversationMode { get; set; } = "workspace";

    public string ResponseStyle { get; set; } = "balanced";

    public bool EnableSmartSuggestions { get; set; } = true;

    public bool EnableAppointmentReminders { get; set; } = true;

    public bool EnableMedicineReminders { get; set; } = true;

    public bool EnableTaskReminders { get; set; } = true;

    public bool EnableWeeklySummary { get; set; } = true;

    public bool EnableDesktopNotifications { get; set; }

    public int AutoDeleteTrashDays { get; set; } = 30;

    public string DefaultHeightUnit { get; set; } = "cm";

    public string DefaultWeightUnit { get; set; } = "kg";

    public int ReminderLeadTimeHours { get; set; } = 24;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
