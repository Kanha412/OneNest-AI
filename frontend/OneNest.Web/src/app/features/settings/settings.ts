import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin, finalize } from 'rxjs';

import { SettingsService } from '../../services/settings.service';
import { DocumentsService } from '../../services/documents.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast/toast.service';
import {
  SettingsResponse,
  UpdateSettingsRequest,
  ContextDepth,
  ConversationMode,
  ResponseStyle,
  HeightUnit,
  WeightUnit,
  ThemeMode
} from '../../models/settings.model';
import { DocumentSummary } from '../../models/document.model';
import { Spinner } from '../../shared/spinner/spinner';

type SectionId =
  | 'account'
  | 'ai'
  | 'documents'
  | 'health'
  | 'privacy'
  | 'about';

@Component({
  selector: 'app-settings',
  imports: [ReactiveFormsModule, DatePipe, Spinner],
  templateUrl: './settings.html',
  styleUrl: './settings.css'
})
export class Settings implements OnInit {
  private readonly settingsService = inject(SettingsService);
  private readonly documentsService = inject(DocumentsService);
  private readonly authService = inject(AuthService);
  private readonly toastService = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly activeSection = signal<SectionId>('account');
  readonly lastSavedAt = signal<Date | null>(null);
  readonly currentUser = this.authService.currentUser;

  readonly sections: { id: SectionId; label: string; icon: string }[] = [
    { id: 'account', label: 'Account', icon: '👤' },
    { id: 'ai', label: 'AI Preferences', icon: '🤖' },
    { id: 'documents', label: 'Documents', icon: '📄' },
    { id: 'health', label: 'Health', icon: '🏥' },
    { id: 'privacy', label: 'Privacy & Security', icon: '🔐' },
    { id: 'about', label: 'About', icon: 'ℹ️' }
  ];

  readonly settings = signal<SettingsResponse | null>(null);
  readonly documentSummary = signal<DocumentSummary | null>(null);

  readonly form = this.fb.group({
    displayName: ['', [Validators.required, Validators.maxLength(120)]],
    enableWorkspaceContext: [true],
    contextDepth: ['medium' as ContextDepth, Validators.required],
    defaultConversationMode: ['workspace' as ConversationMode, Validators.required],
    responseStyle: ['balanced' as ResponseStyle, Validators.required],
    enableSmartSuggestions: [true],
    enableAppointmentReminders: [true],
    enableMedicineReminders: [true],
    enableTaskReminders: [true],
    enableWeeklySummary: [true],
    enableDesktopNotifications: [false],
    autoDeleteTrashDays: [30, [Validators.required, Validators.min(1), Validators.max(365)]],
    defaultHeightUnit: ['cm' as HeightUnit, Validators.required],
    defaultWeightUnit: ['kg' as WeightUnit, Validators.required],
    reminderLeadTimeHours: [24, [Validators.required, Validators.min(1), Validators.max(168)]],
    theme: ['system' as ThemeMode, Validators.required],
    compactSidebar: [false],
    enableAnimations: [true]
  });

  ngOnInit(): void {
    this.load();
  }

  setSection(id: SectionId): void {
    this.activeSection.set(id);
  }

  load(): void {
    this.isLoading.set(true);

    forkJoin({
      settings: this.settingsService.getSettings(),
      docSummary: this.documentsService.getSummary()
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ settings, docSummary }) => {
          this.settings.set(settings);
          this.documentSummary.set(docSummary);
          this.patchForm(settings);
        },
        error: () => this.toastService.error('Failed to load settings')
      });
  }

  save(): void {
    if (!this.hasUnsavedChanges()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const request: UpdateSettingsRequest = {
      displayName: (this.form.value.displayName ?? '').trim(),
      enableWorkspaceContext: !!this.form.value.enableWorkspaceContext,
      contextDepth: this.form.value.contextDepth ?? 'medium',
      defaultConversationMode: this.form.value.defaultConversationMode ?? 'workspace',
      responseStyle: this.form.value.responseStyle ?? 'balanced',
      enableSmartSuggestions: !!this.form.value.enableSmartSuggestions,
      enableAppointmentReminders: !!this.form.value.enableAppointmentReminders,
      enableMedicineReminders: !!this.form.value.enableMedicineReminders,
      enableTaskReminders: !!this.form.value.enableTaskReminders,
      enableWeeklySummary: !!this.form.value.enableWeeklySummary,
      enableDesktopNotifications: !!this.form.value.enableDesktopNotifications,
      autoDeleteTrashDays: Number(this.form.value.autoDeleteTrashDays ?? 30),
      defaultHeightUnit: this.form.value.defaultHeightUnit ?? 'cm',
      defaultWeightUnit: this.form.value.defaultWeightUnit ?? 'kg',
      reminderLeadTimeHours: Number(this.form.value.reminderLeadTimeHours ?? 24),
      theme: this.form.value.theme ?? 'system',
      compactSidebar: !!this.form.value.compactSidebar,
      enableAnimations: !!this.form.value.enableAnimations
    };

    this.isSaving.set(true);
    this.settingsService.updateSettings(request)
      .pipe(finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: response => {
          this.settings.set(response);
          this.patchForm(response);
          this.form.markAsPristine();
          this.lastSavedAt.set(new Date());
          this.authService.updateCurrentUserProfile(response.account.displayName, response.account.email);
          this.toastService.success('Settings saved successfully');
        },
        error: err => {
          const message = typeof err?.error === 'string' ? err.error : 'Failed to save settings';
          this.toastService.error(message);
        }
      });
  }

  placeholder(name: string): void {
    this.toastService.info(`${name} is coming soon`);
  }

  formatStorage(bytes: number): string {
    if (!bytes) {
      return '0 B';
    }

    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = bytes;
    let unitIndex = 0;

    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }

    return `${size.toFixed(size >= 100 ? 0 : 1)} ${units[unitIndex]}`;
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty;
  }

  getLastSavedText(): string {
    const savedAt = this.lastSavedAt();
    if (!savedAt) {
      return '';
    }

    const seconds = Math.max(0, Math.floor((Date.now() - savedAt.getTime()) / 1000));
    if (seconds < 10) {
      return 'Last updated just now';
    }

    if (seconds < 60) {
      return `Last updated ${seconds}s ago`;
    }

    return `Last saved: ${savedAt.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}`;
  }

  private patchForm(value: SettingsResponse): void {
    this.form.patchValue({
      displayName: value.account.displayName,
      enableWorkspaceContext: value.aiPreferences.enableWorkspaceContext,
      contextDepth: value.aiPreferences.contextDepth,
      defaultConversationMode: value.aiPreferences.defaultConversationMode,
      responseStyle: value.aiPreferences.responseStyle,
      enableSmartSuggestions: value.aiPreferences.enableSmartSuggestions,
      enableAppointmentReminders: value.notifications.enableAppointmentReminders,
      enableMedicineReminders: value.notifications.enableMedicineReminders,
      enableTaskReminders: value.notifications.enableTaskReminders,
      enableWeeklySummary: value.notifications.enableWeeklySummary,
      enableDesktopNotifications: value.notifications.enableDesktopNotifications,
      autoDeleteTrashDays: value.documents.autoDeleteTrashDays,
      defaultHeightUnit: value.health.defaultHeightUnit,
      defaultWeightUnit: value.health.defaultWeightUnit,
      reminderLeadTimeHours: value.health.reminderLeadTimeHours,
      theme: value.appearance.theme,
      compactSidebar: value.appearance.compactSidebar,
      enableAnimations: value.appearance.enableAnimations
    });

    this.form.markAsPristine();
  }
}
