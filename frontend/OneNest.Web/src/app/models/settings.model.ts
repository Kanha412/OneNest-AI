export type ContextDepth = 'low' | 'medium' | 'high';
export type ConversationMode = 'workspace' | 'general';
export type ResponseStyle = 'short' | 'balanced' | 'detailed';
export type ThemeMode = 'light' | 'dark' | 'system';
export type HeightUnit = 'cm' | 'ft';
export type WeightUnit = 'kg' | 'lb';

export interface SettingsResponse {
  account: AccountSettings;
  aiPreferences: AiPreferencesSettings;
  notifications: NotificationSettings;
  documents: DocumentSettings;
  health: HealthSettings;
  appearance: AppearanceSettings;
  privacy: PrivacySettings;
  about: AboutSettings;
}

export interface AccountSettings {
  displayName: string;
  email: string;
  memberSince: string;
  lastLoginAt: string | null;
}

export interface AiPreferencesSettings {
  enableWorkspaceContext: boolean;
  contextDepth: ContextDepth;
  defaultConversationMode: ConversationMode;
  responseStyle: ResponseStyle;
  enableSmartSuggestions: boolean;
}

export interface NotificationSettings {
  enableAppointmentReminders: boolean;
  enableMedicineReminders: boolean;
  enableTaskReminders: boolean;
  enableWeeklySummary: boolean;
  enableDesktopNotifications: boolean;
}

export interface DocumentSettings {
  autoDeleteTrashDays: number;
}

export interface HealthSettings {
  defaultHeightUnit: HeightUnit;
  defaultWeightUnit: WeightUnit;
  reminderLeadTimeHours: number;
}

export interface AppearanceSettings {
  theme: ThemeMode;
  compactSidebar: boolean;
  enableAnimations: boolean;
}

export interface PrivacySettings {
  canChangePassword: boolean;
  canExportData: boolean;
  canDeleteAccount: boolean;
  hasLoggedInDevices: boolean;
}

export interface AboutSettings {
  applicationVersion: string;
  buildVersion: string;
  developer: string;
  copyright: string;
}

export interface UpdateSettingsRequest {
  displayName: string;
  enableWorkspaceContext: boolean;
  contextDepth: ContextDepth;
  defaultConversationMode: ConversationMode;
  responseStyle: ResponseStyle;
  enableSmartSuggestions: boolean;
  enableAppointmentReminders: boolean;
  enableMedicineReminders: boolean;
  enableTaskReminders: boolean;
  enableWeeklySummary: boolean;
  enableDesktopNotifications: boolean;
  autoDeleteTrashDays: number;
  defaultHeightUnit: HeightUnit;
  defaultWeightUnit: WeightUnit;
  reminderLeadTimeHours: number;
  theme: ThemeMode;
  compactSidebar: boolean;
  enableAnimations: boolean;
}
