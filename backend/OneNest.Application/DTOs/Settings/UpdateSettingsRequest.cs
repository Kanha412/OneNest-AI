using System.ComponentModel.DataAnnotations;

namespace OneNest.Application.DTOs.Settings;

public class UpdateSettingsRequest
{
    [Required]
    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    public bool EnableWorkspaceContext { get; set; } = true;

    [Required]
    [RegularExpression("^(low|medium|high)$")]
    public string ContextDepth { get; set; } = "medium";

    [Required]
    [RegularExpression("^(workspace|general)$")]
    public string DefaultConversationMode { get; set; } = "workspace";

    [Required]
    [RegularExpression("^(short|balanced|detailed)$")]
    public string ResponseStyle { get; set; } = "balanced";

    public bool EnableSmartSuggestions { get; set; } = true;

    public bool EnableAppointmentReminders { get; set; } = true;

    public bool EnableMedicineReminders { get; set; } = true;

    public bool EnableTaskReminders { get; set; } = true;

    public bool EnableWeeklySummary { get; set; } = true;

    public bool EnableDesktopNotifications { get; set; }

    [Range(1, 365)]
    public int AutoDeleteTrashDays { get; set; } = 30;

    [Required]
    [RegularExpression("^(cm|ft)$")]
    public string DefaultHeightUnit { get; set; } = "cm";

    [Required]
    [RegularExpression("^(kg|lb)$")]
    public string DefaultWeightUnit { get; set; } = "kg";

    [Range(1, 168)]
    public int ReminderLeadTimeHours { get; set; } = 24;

    [Required]
    [RegularExpression("^(light|dark|system)$")]
    public string Theme { get; set; } = "system";

    public bool CompactSidebar { get; set; }

    public bool EnableAnimations { get; set; } = true;
}
