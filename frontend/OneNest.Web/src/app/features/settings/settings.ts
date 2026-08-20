import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin, finalize } from 'rxjs';

import { SettingsService } from '../../services/settings.service';
import { DocumentsService } from '../../services/documents.service';
import { HealthHubService } from '../../services/health-hub.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast/toast.service';
import { ConfirmService } from '../../shared/confirm/confirm.service';
import {
  SettingsResponse,
  UpdateSettingsRequest,
  HeightUnit,
  WeightUnit,
  ThemeMode
} from '../../models/settings.model';
import { DocumentSummary } from '../../models/document.model';
import { MedicalReport } from '../../models/health.model';
import { Spinner } from '../../shared/spinner/spinner';

type SectionId =
  | 'account'
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
  private readonly healthHubService = inject(HealthHubService);
  private readonly authService = inject(AuthService);
  private readonly toastService = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  private readonly storageLimitBytes = 150 * 1024 * 1024;

  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly isChangingPassword = signal(false);
  readonly isDeletingAccount = signal(false);
  readonly showChangePasswordModal = signal(false);
  readonly showDeleteAccountModal = signal(false);
  readonly activeSection = signal<SectionId>('account');
  readonly lastSavedAt = signal<Date | null>(null);
  readonly currentUser = this.authService.currentUser;

  readonly sections: { id: SectionId; label: string; icon: string }[] = [
    { id: 'account', label: 'Account', icon: '👤' },
    { id: 'documents', label: 'Storage', icon: '🗄️' },
    { id: 'health', label: 'Measurements', icon: '📏' },
    { id: 'privacy', label: 'Security', icon: '🔐' },
    { id: 'about', label: 'About', icon: 'ℹ️' }
  ];

  readonly settings = signal<SettingsResponse | null>(null);
  readonly documentSummary = signal<DocumentSummary | null>(null);
  readonly totalStorageUsedBytes = signal(0);
  readonly totalStoredFiles = signal(0);

  readonly form = this.fb.group({
    displayName: ['', [Validators.required, Validators.maxLength(120)]],
    enableAppointmentReminders: [true],
    enableMedicineReminders: [true],
    enableTaskReminders: [true],
    enableWeeklySummary: [true],
    enableDesktopNotifications: [false],
    defaultHeightUnit: ['cm' as HeightUnit, Validators.required],
    defaultWeightUnit: ['kg' as WeightUnit, Validators.required],
    theme: ['system' as ThemeMode, Validators.required],
    compactSidebar: [false],
    enableAnimations: [true]
  });

  readonly changePasswordForm = this.fb.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', [Validators.required]]
  });

  readonly deleteAccountForm = this.fb.group({
    password: ['', [Validators.required]]
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
      docSummary: this.documentsService.getSummary(),
      reports: this.healthHubService.getReports()
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ settings, docSummary, reports }) => {
          this.settings.set(settings);
          this.documentSummary.set(docSummary);
          this.patchForm(settings);
          this.refreshStorageTotals(docSummary, reports);
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

    const currentSettings = this.settings();

    const request: UpdateSettingsRequest = {
      displayName: (this.form.value.displayName ?? '').trim(),
      enableAppointmentReminders: !!this.form.value.enableAppointmentReminders,
      enableMedicineReminders: !!this.form.value.enableMedicineReminders,
      enableTaskReminders: !!this.form.value.enableTaskReminders,
      enableWeeklySummary: !!this.form.value.enableWeeklySummary,
      enableDesktopNotifications: !!this.form.value.enableDesktopNotifications,
      autoDeleteTrashDays: currentSettings?.documents.autoDeleteTrashDays ?? 30,
      defaultHeightUnit: this.form.value.defaultHeightUnit ?? 'cm',
      defaultWeightUnit: this.form.value.defaultWeightUnit ?? 'kg',
      reminderLeadTimeHours: currentSettings?.health.reminderLeadTimeHours ?? 24,
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

  openPrivacyPolicy(): void {
    this.router.navigate(['/privacy-policy']);
  }

  openTerms(): void {
    this.router.navigate(['/terms']);
  }

  openGithub(): void {
    window.open('https://github.com/Kanha412/OneNest-AI', '_blank', 'noopener,noreferrer');
  }

  openChangePasswordModal(): void {
    this.deleteAccountForm.reset();
    this.showDeleteAccountModal.set(false);
    this.changePasswordForm.reset();
    this.showChangePasswordModal.set(true);
  }

  closeChangePasswordModal(): void {
    this.showChangePasswordModal.set(false);
  }

  async promptDeleteAccountFlow(): Promise<void> {
    this.changePasswordForm.reset();
    this.showChangePasswordModal.set(false);
    this.deleteAccountForm.reset();

    const confirmed = await this.confirmService.confirm({
      title: 'Delete account permanently',
      message: 'This will permanently delete your account and all your data. This action cannot be undone.',
      confirmText: 'Delete Permanently',
      cancelText: 'Cancel'
    });

    if (!confirmed) {
      return;
    }

    this.showDeleteAccountModal.set(true);
  }

  closeDeleteAccountModal(): void {
    this.showDeleteAccountModal.set(false);
  }

  changePassword(): void {
    if (this.changePasswordForm.invalid) {
      this.changePasswordForm.markAllAsTouched();
      return;
    }

    const currentPassword = this.changePasswordForm.value.currentPassword ?? '';
    const newPassword = this.changePasswordForm.value.newPassword ?? '';
    const confirmPassword = this.changePasswordForm.value.confirmPassword ?? '';

    if (newPassword !== confirmPassword) {
      this.toastService.error('Confirm password must match new password');
      return;
    }

    this.isChangingPassword.set(true);
    this.authService.changePassword({ currentPassword, newPassword, confirmPassword })
      .pipe(finalize(() => this.isChangingPassword.set(false)))
      .subscribe({
        next: response => {
          this.changePasswordForm.reset();
          this.showChangePasswordModal.set(false);
          this.toastService.success(response?.message || 'Password changed successfully');
        },
        error: err => {
          const message = typeof err?.error?.message === 'string'
            ? err.error.message
            : 'Failed to change password';
          this.toastService.error(message);
        }
      });
  }

  deleteAccount(): void {
    if (this.deleteAccountForm.invalid) {
      this.deleteAccountForm.markAllAsTouched();
      return;
    }

    const password = this.deleteAccountForm.value.password ?? '';
    this.isDeletingAccount.set(true);

    this.authService.deleteAccount({ password })
      .pipe(finalize(() => this.isDeletingAccount.set(false)))
      .subscribe({
        next: response => {
          this.deleteAccountForm.reset();
          this.showDeleteAccountModal.set(false);
          this.authService.logout();
          this.toastService.success(response?.message || 'Account deleted successfully');
          this.router.navigate(['/login']);
        },
        error: err => {
          const message = typeof err?.error?.message === 'string'
            ? err.error.message
            : 'Failed to delete account';
          this.toastService.error(message);
        }
      });
  }

  getStorageUsageText(): string {
    return `${this.formatStorage(this.totalStorageUsedBytes())} / ${this.formatStorage(this.storageLimitBytes)}`;
  }

  getStorageLeftText(): string {
    const leftBytes = Math.max(0, this.storageLimitBytes - this.totalStorageUsedBytes());
    const leftMb = leftBytes / (1024 * 1024);
    return `${leftMb.toFixed(1)} MB left`;
  }

  downloadAllStorageFiles(): void {
    this.documentsService.downloadAllFiles().subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;

        const fullName = (this.currentUser()?.fullName || this.settings()?.account?.displayName || 'User').trim();
        const sanitizedName = fullName
          .replace(/[^a-zA-Z0-9\s_-]/g, '')
          .replace(/\s+/g, '')
          .slice(0, 40) || 'User';

        const now = new Date();
        const yyyy = now.getFullYear();
        const mm = String(now.getMonth() + 1).padStart(2, '0');
        const dd = String(now.getDate()).padStart(2, '0');
        const hh = String(now.getHours()).padStart(2, '0');
        const min = String(now.getMinutes()).padStart(2, '0');

        link.download = `${sanitizedName}_OneNest_StorageArchive_${yyyy}${mm}${dd}_${hh}${min}.zip`;
        link.click();
        URL.revokeObjectURL(url);
        this.toastService.success('Storage archive downloaded');
      },
      error: err => {
        if (err?.status === 404) {
          this.toastService.info('No storage data available to download');
          return;
        }

        const message = typeof err?.error === 'string' ? err.error : 'Failed to download storage archive';
        this.toastService.error(message);
      }
    });
  }

  async clearAllStorageFiles(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Clear storage',
      message: 'Are you sure you want to delete all stored files (documents and health reports)? This action cannot be undone.',
      confirmText: 'Clear Storage',
      cancelText: 'Cancel'
    });

    if (!confirmed) {
      return;
    }

    this.documentsService.deleteAllDocuments().subscribe({
      next: result => {
        this.documentSummary.update(current => {
          if (!current) {
            return current;
          }

          return {
            ...current,
            totalDocuments: 0,
            todayUploads: 0,
            storageUsed: 0,
            recentDocuments: [],
            categoryDistribution: []
          };
        });

        this.totalStorageUsedBytes.set(0);
        this.totalStoredFiles.set(0);

        const deletedCount = Number(result?.deletedCount ?? 0);
        this.toastService.success(deletedCount > 0 ? `${deletedCount} files deleted from storage` : 'No files to delete');
      },
      error: err => {
        const message = typeof err?.error === 'string' ? err.error : 'Failed to clear storage files';
        this.toastService.error(message);
      }
    });
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

  private refreshStorageTotals(docSummary: DocumentSummary, reports: MedicalReport[]): void {
    const documentBytes = docSummary?.storageUsed ?? 0;
    const reportBytes = reports.reduce((sum, report) => sum + Number(report.fileSize ?? 0), 0);
    const totalBytes = documentBytes + reportBytes;

    this.totalStorageUsedBytes.set(totalBytes);
    this.totalStoredFiles.set((docSummary?.totalDocuments ?? 0) + reports.length);
  }

  private patchForm(value: SettingsResponse): void {
    this.form.patchValue({
      displayName: value.account.displayName,
      enableAppointmentReminders: value.notifications.enableAppointmentReminders,
      enableMedicineReminders: value.notifications.enableMedicineReminders,
      enableTaskReminders: value.notifications.enableTaskReminders,
      enableWeeklySummary: value.notifications.enableWeeklySummary,
      enableDesktopNotifications: value.notifications.enableDesktopNotifications,
      defaultHeightUnit: value.health.defaultHeightUnit,
      defaultWeightUnit: value.health.defaultWeightUnit,
      theme: value.appearance.theme,
      compactSidebar: value.appearance.compactSidebar,
      enableAnimations: value.appearance.enableAnimations
    });

    this.form.markAsPristine();
  }
}
