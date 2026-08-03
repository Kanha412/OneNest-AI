import {
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';

import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import { DatePipe } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { finalize } from 'rxjs';

import { HealthHubService } from '../../services/health-hub.service';
import {
  Medicine,
  Appointment,
  MedicalRecord,
  MedicalReport,
  AppointmentStatus,
  APPOINTMENT_STATUS_LABELS,
  MedicineFoodTiming,
  FOOD_TIMING_LABELS,
  MedicalReportCategory,
  REPORT_CATEGORY_LABELS
} from '../../models/health.model';
import { Spinner } from '../../shared/spinner/spinner';
import { ConfirmService } from '../../shared/confirm/confirm.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Paginator } from '../../shared/paginator/paginator';

type HealthTab = 'medicines' | 'appointments' | 'records' | 'reports';

@Component({
  selector: 'app-health',
  imports: [ReactiveFormsModule, DatePipe, Spinner, Paginator],
  templateUrl: './health.html',
  styleUrl: './health.css'
})
export class Health implements OnInit {

  private readonly service = inject(HealthHubService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastService = inject(ToastService);
  private readonly sanitizer = inject(DomSanitizer);

  // ---------- Labels / enum lists ----------
  readonly foodTimingLabels = FOOD_TIMING_LABELS;
  readonly statusLabels = APPOINTMENT_STATUS_LABELS;
  readonly reportCategoryLabels = REPORT_CATEGORY_LABELS;

  readonly foodTimings = [
    MedicineFoodTiming.BeforeFood,
    MedicineFoodTiming.AfterFood,
    MedicineFoodTiming.Anytime
  ];

  readonly statuses = [
    AppointmentStatus.Scheduled,
    AppointmentStatus.Completed,
    AppointmentStatus.Cancelled
  ];

  readonly reportCategories = [
    MedicalReportCategory.LabReport,
    MedicalReportCategory.Prescription,
    MedicalReportCategory.Scan,
    MedicalReportCategory.DischargeSummary,
    MedicalReportCategory.Other
  ];

  readonly activeTab = signal<HealthTab>('medicines');

  setTab(tab: HealthTab): void {
    this.activeTab.set(tab);
  }

  ngOnInit(): void {
    this.loadMedicines();
    this.loadAppointments();
    this.loadRecord();
    this.loadReports();
  }

  // =====================================================
  // MEDICINES
  // =====================================================
  readonly medicines = signal<Medicine[]>([]);
  readonly medicinesLoading = signal(false);
  readonly medicineSaving = signal(false);
  readonly editingMedicineId = signal<string | null>(null);
  readonly medicineSearch = signal('');
  readonly medicineActiveFilter = signal<'all' | 'active' | 'inactive'>('all');
  readonly medicinePage = signal(1);
  readonly medicinePageSize = 6;

  readonly medicineForm = this.fb.group({
    name: ['', Validators.required],
    dosage: [''],
    frequency: [''],
    morning: [false],
    afternoon: [false],
    night: [false],
    startDate: [this.today(), Validators.required],
    endDate: [''],
    instructions: [''],
    foodTiming: [MedicineFoodTiming.Anytime],
    isActive: [true]
  });

  readonly filteredMedicines = computed(() => {
    const text = this.medicineSearch().toLowerCase().trim();
    const filter = this.medicineActiveFilter();

    return this.medicines().filter(m => {
      const matchesText =
        m.name.toLowerCase().includes(text) ||
        (m.frequency ?? '').toLowerCase().includes(text) ||
        (m.instructions ?? '').toLowerCase().includes(text);

      const matchesActive =
        filter === 'all' ||
        (filter === 'active' && m.isActive) ||
        (filter === 'inactive' && !m.isActive);

      return matchesText && matchesActive;
    });
  });

  readonly medicineTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredMedicines().length / this.medicinePageSize))
  );

  readonly pagedMedicines = computed(() => {
    const page = Math.min(this.medicinePage(), this.medicineTotalPages());
    const start = (page - 1) * this.medicinePageSize;
    return this.filteredMedicines().slice(start, start + this.medicinePageSize);
  });

  onMedicineSearch(value: string): void {
    this.medicineSearch.set(value);
    this.medicinePage.set(1);
  }

  onMedicineActiveFilter(value: string): void {
    this.medicineActiveFilter.set(value as 'all' | 'active' | 'inactive');
    this.medicinePage.set(1);
  }

  loadMedicines(): void {
    this.medicinesLoading.set(true);
    this.service.getMedicines()
      .pipe(finalize(() => this.medicinesLoading.set(false)))
      .subscribe({
        next: data => this.medicines.set(data),
        error: () => this.toastService.error('Failed to load medicines')
      });
  }

  saveMedicine(): void {
    if (this.medicineForm.invalid) {
      this.medicineForm.markAllAsTouched();
      return;
    }

    const v = this.medicineForm.value;
    const payload = {
      name: v.name!,
      dosage: v.dosage ?? '',
      frequency: v.frequency ?? '',
      morning: !!v.morning,
      afternoon: !!v.afternoon,
      night: !!v.night,
      startDate: v.startDate!,
      endDate: v.endDate ? v.endDate : null,
      instructions: v.instructions ?? '',
      foodTiming: Number(v.foodTiming),
      isActive: !!v.isActive
    };

    this.medicineSaving.set(true);

    const request = this.editingMedicineId()
      ? this.service.updateMedicine(this.editingMedicineId()!, payload)
      : this.service.createMedicine(payload);

    request.pipe(finalize(() => this.medicineSaving.set(false)))
      .subscribe({
        next: () => {
          this.toastService.success(this.editingMedicineId() ? 'Medicine updated' : 'Medicine added');
          this.resetMedicineForm();
          this.loadMedicines();
        },
        error: err => this.toastService.error(
          typeof err?.error === 'string' ? err.error : 'Failed to save medicine'
        )
      });
  }

  editMedicine(m: Medicine): void {
    this.editingMedicineId.set(m.id);
    this.medicineForm.reset({
      name: m.name,
      dosage: m.dosage,
      frequency: m.frequency,
      morning: m.morning,
      afternoon: m.afternoon,
      night: m.night,
      startDate: m.startDate?.substring(0, 10),
      endDate: m.endDate ? m.endDate.substring(0, 10) : '',
      instructions: m.instructions,
      foodTiming: m.foodTiming,
      isActive: m.isActive
    });
  }

  async deleteMedicine(m: Medicine): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Delete Medicine',
      message: `Delete "${m.name}"?`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });
    if (!confirmed) return;

    this.service.deleteMedicine(m.id).subscribe({
      next: () => {
        this.toastService.success('Medicine deleted');
        this.loadMedicines();
      },
      error: () => this.toastService.error('Failed to delete medicine')
    });
  }

  cancelMedicineEdit(): void {
    this.resetMedicineForm();
  }

  private resetMedicineForm(): void {
    this.editingMedicineId.set(null);
    this.medicineForm.reset({
      name: '',
      dosage: '',
      frequency: '',
      morning: false,
      afternoon: false,
      night: false,
      startDate: this.today(),
      endDate: '',
      instructions: '',
      foodTiming: MedicineFoodTiming.Anytime,
      isActive: true
    });
  }

  timingBadges(m: Medicine): string {
    const parts: string[] = [];
    if (m.morning) parts.push('Morning');
    if (m.afternoon) parts.push('Afternoon');
    if (m.night) parts.push('Night');
    return parts.length ? parts.join(' · ') : '—';
  }

  // =====================================================
  // APPOINTMENTS
  // =====================================================
  readonly appointments = signal<Appointment[]>([]);
  readonly appointmentsLoading = signal(false);
  readonly appointmentSaving = signal(false);
  readonly editingAppointmentId = signal<string | null>(null);
  readonly appointmentSearch = signal('');
  readonly appointmentStatusFilter = signal<number | 'all'>('all');
  readonly appointmentPage = signal(1);
  readonly appointmentPageSize = 6;

  readonly appointmentForm = this.fb.group({
    doctorName: ['', Validators.required],
    hospital: [''],
    specialty: [''],
    date: [this.today(), Validators.required],
    time: ['09:00', Validators.required],
    notes: [''],
    status: [AppointmentStatus.Scheduled]
  });

  readonly filteredAppointments = computed(() => {
    const text = this.appointmentSearch().toLowerCase().trim();
    const status = this.appointmentStatusFilter();

    return this.appointments().filter(a => {
      const matchesText =
        a.doctorName.toLowerCase().includes(text) ||
        (a.hospital ?? '').toLowerCase().includes(text) ||
        (a.specialty ?? '').toLowerCase().includes(text);

      const matchesStatus = status === 'all' || a.status === status;

      return matchesText && matchesStatus;
    });
  });

  readonly appointmentTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredAppointments().length / this.appointmentPageSize))
  );

  readonly pagedAppointments = computed(() => {
    const page = Math.min(this.appointmentPage(), this.appointmentTotalPages());
    const start = (page - 1) * this.appointmentPageSize;
    return this.filteredAppointments().slice(start, start + this.appointmentPageSize);
  });

  onAppointmentSearch(value: string): void {
    this.appointmentSearch.set(value);
    this.appointmentPage.set(1);
  }

  onAppointmentStatusFilter(value: string): void {
    this.appointmentStatusFilter.set(value === 'all' ? 'all' : Number(value));
    this.appointmentPage.set(1);
  }

  loadAppointments(): void {
    this.appointmentsLoading.set(true);
    this.service.getAppointments()
      .pipe(finalize(() => this.appointmentsLoading.set(false)))
      .subscribe({
        next: data => this.appointments.set(data),
        error: () => this.toastService.error('Failed to load appointments')
      });
  }

  saveAppointment(): void {
    if (this.appointmentForm.invalid) {
      this.appointmentForm.markAllAsTouched();
      return;
    }

    const v = this.appointmentForm.value;
    const payload = {
      doctorName: v.doctorName!,
      hospital: v.hospital ?? '',
      specialty: v.specialty ?? '',
      date: v.date!,
      time: v.time!.length === 5 ? `${v.time}:00` : v.time!,
      notes: v.notes ?? '',
      status: Number(v.status)
    };

    this.appointmentSaving.set(true);

    const request = this.editingAppointmentId()
      ? this.service.updateAppointment(this.editingAppointmentId()!, payload)
      : this.service.createAppointment(payload);

    request.pipe(finalize(() => this.appointmentSaving.set(false)))
      .subscribe({
        next: () => {
          this.toastService.success(this.editingAppointmentId() ? 'Appointment updated' : 'Appointment added');
          this.resetAppointmentForm();
          this.loadAppointments();
        },
        error: err => this.toastService.error(
          typeof err?.error === 'string' ? err.error : 'Failed to save appointment'
        )
      });
  }

  editAppointment(a: Appointment): void {
    this.editingAppointmentId.set(a.id);
    this.appointmentForm.reset({
      doctorName: a.doctorName,
      hospital: a.hospital,
      specialty: a.specialty,
      date: a.date?.substring(0, 10),
      time: a.time?.substring(0, 5),
      notes: a.notes,
      status: a.status
    });
  }

  async deleteAppointment(a: Appointment): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Delete Appointment',
      message: `Delete appointment with ${a.doctorName}?`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });
    if (!confirmed) return;

    this.service.deleteAppointment(a.id).subscribe({
      next: () => {
        this.toastService.success('Appointment deleted');
        this.loadAppointments();
      },
      error: () => this.toastService.error('Failed to delete appointment')
    });
  }

  cancelAppointmentEdit(): void {
    this.resetAppointmentForm();
  }

  private resetAppointmentForm(): void {
    this.editingAppointmentId.set(null);
    this.appointmentForm.reset({
      doctorName: '',
      hospital: '',
      specialty: '',
      date: this.today(),
      time: '09:00',
      notes: '',
      status: AppointmentStatus.Scheduled
    });
  }

  statusClass(status: AppointmentStatus): string {
    switch (status) {
      case AppointmentStatus.Completed: return 'status-completed';
      case AppointmentStatus.Cancelled: return 'status-cancelled';
      default: return 'status-scheduled';
    }
  }

  // =====================================================
  // MEDICAL RECORD
  // =====================================================
  readonly recordLoading = signal(false);
  readonly recordSaving = signal(false);
  readonly hasRecord = signal(false);

  readonly recordForm = this.fb.group({
    bloodGroup: [''],
    heightCm: [null as number | null],
    weightKg: [null as number | null],
    allergies: [''],
    existingConditions: [''],
    emergencyContactName: [''],
    emergencyContactPhone: ['']
  });

  loadRecord(): void {
    this.recordLoading.set(true);
    this.service.getMedicalRecord()
      .pipe(finalize(() => this.recordLoading.set(false)))
      .subscribe({
        next: (r: MedicalRecord) => {
          if (r) {
            this.hasRecord.set(true);
            this.recordForm.patchValue({
              bloodGroup: r.bloodGroup,
              heightCm: r.heightCm,
              weightKg: r.weightKg,
              allergies: r.allergies,
              existingConditions: r.existingConditions,
              emergencyContactName: r.emergencyContactName,
              emergencyContactPhone: r.emergencyContactPhone
            });
          }
        },
        error: () => { /* 204 No Content: no record yet */ }
      });
  }

  saveRecord(): void {
    const v = this.recordForm.value;
    const payload = {
      bloodGroup: v.bloodGroup ?? '',
      heightCm: v.heightCm ?? null,
      weightKg: v.weightKg ?? null,
      allergies: v.allergies ?? '',
      existingConditions: v.existingConditions ?? '',
      emergencyContactName: v.emergencyContactName ?? '',
      emergencyContactPhone: v.emergencyContactPhone ?? ''
    };

    this.recordSaving.set(true);
    this.service.saveMedicalRecord(payload)
      .pipe(finalize(() => this.recordSaving.set(false)))
      .subscribe({
        next: () => {
          this.hasRecord.set(true);
          this.toastService.success('Health record saved');
        },
        error: () => this.toastService.error('Failed to save health record')
      });
  }

  // =====================================================
  // MEDICAL REPORTS
  // =====================================================
  readonly reports = signal<MedicalReport[]>([]);
  readonly reportsLoading = signal(false);
  readonly reportSaving = signal(false);
  readonly editingReportId = signal<string | null>(null);
  readonly reportSearch = signal('');
  readonly reportCategoryFilter = signal<number | 'all'>('all');
  readonly reportPage = signal(1);
  readonly reportPageSize = 6;
  readonly selectedReportFile = signal<File | null>(null);

  readonly previewSafeUrl = signal<SafeResourceUrl | null>(null);
  private previewObjectUrl: string | null = null;
  readonly previewType = signal<string>('');
  readonly previewName = signal<string>('');
  readonly showPreview = signal(false);

  readonly reportForm = this.fb.group({
    title: ['', Validators.required],
    category: [MedicalReportCategory.Other],
    doctorName: [''],
    hospital: [''],
    reportDate: [this.today(), Validators.required],
    description: ['']
  });

  readonly filteredReports = computed(() => {
    const text = this.reportSearch().toLowerCase().trim();
    const category = this.reportCategoryFilter();

    return this.reports().filter(r => {
      const matchesText =
        r.title.toLowerCase().includes(text) ||
        (r.doctorName ?? '').toLowerCase().includes(text) ||
        (r.hospital ?? '').toLowerCase().includes(text) ||
        (r.description ?? '').toLowerCase().includes(text);

      const matchesCategory = category === 'all' || r.category === category;

      return matchesText && matchesCategory;
    });
  });

  readonly reportTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredReports().length / this.reportPageSize))
  );

  readonly pagedReports = computed(() => {
    const page = Math.min(this.reportPage(), this.reportTotalPages());
    const start = (page - 1) * this.reportPageSize;
    return this.filteredReports().slice(start, start + this.reportPageSize);
  });

  onReportSearch(value: string): void {
    this.reportSearch.set(value);
    this.reportPage.set(1);
  }

  onReportCategoryFilter(value: string): void {
    this.reportCategoryFilter.set(value === 'all' ? 'all' : Number(value));
    this.reportPage.set(1);
  }

  onReportFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedReportFile.set(file);

    if (file && !this.reportForm.value.title) {
      const nameWithoutExt = file.name.replace(/\.[^/.]+$/, '');
      this.reportForm.patchValue({ title: nameWithoutExt });
    }
  }

  loadReports(): void {
    this.reportsLoading.set(true);
    this.service.getReports()
      .pipe(finalize(() => this.reportsLoading.set(false)))
      .subscribe({
        next: data => this.reports.set(data),
        error: () => this.toastService.error('Failed to load reports')
      });
  }

  saveReport(): void {
    if (this.reportForm.invalid) {
      this.reportForm.markAllAsTouched();
      return;
    }

    const v = this.reportForm.value;

    if (this.editingReportId()) {
      this.reportSaving.set(true);
      this.service.updateReport(this.editingReportId()!, {
        title: v.title!,
        category: Number(v.category),
        doctorName: v.doctorName ?? '',
        hospital: v.hospital ?? '',
        reportDate: v.reportDate!,
        description: v.description ?? ''
      })
        .pipe(finalize(() => this.reportSaving.set(false)))
        .subscribe({
          next: () => {
            this.toastService.success('Report updated');
            this.resetReportForm();
            this.loadReports();
          },
          error: () => this.toastService.error('Failed to update report')
        });
      return;
    }

    const file = this.selectedReportFile();
    if (!file) {
      this.toastService.error('Please choose a file to upload');
      return;
    }

    const maxBytes = 25 * 1024 * 1024;
    if (file.size > maxBytes) {
      this.toastService.error('File size exceeds the 25 MB limit');
      return;
    }

    this.reportSaving.set(true);
    this.service.uploadReport(
      file,
      v.title!,
      Number(v.category),
      v.doctorName ?? '',
      v.hospital ?? '',
      v.reportDate!,
      v.description ?? ''
    )
      .pipe(finalize(() => this.reportSaving.set(false)))
      .subscribe({
        next: () => {
          this.toastService.success('Report uploaded');
          this.resetReportForm();
          this.loadReports();
        },
        error: err => this.toastService.error(
          typeof err?.error === 'string' ? err.error : 'Failed to upload report'
        )
      });
  }

  editReport(r: MedicalReport): void {
    this.editingReportId.set(r.id);
    this.reportForm.reset({
      title: r.title,
      category: r.category,
      doctorName: r.doctorName,
      hospital: r.hospital,
      reportDate: r.reportDate?.substring(0, 10),
      description: r.description
    });
  }

  async deleteReport(r: MedicalReport): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Delete Report',
      message: `Delete "${r.title}"?`,
      confirmText: 'Delete',
      cancelText: 'Cancel'
    });
    if (!confirmed) return;

    this.service.deleteReport(r.id).subscribe({
      next: () => {
        this.toastService.success('Report deleted');
        this.loadReports();
      },
      error: () => this.toastService.error('Failed to delete report')
    });
  }

  cancelReportEdit(): void {
    this.resetReportForm();
  }

  private resetReportForm(): void {
    this.editingReportId.set(null);
    this.selectedReportFile.set(null);
    this.reportForm.reset({
      title: '',
      category: MedicalReportCategory.Other,
      doctorName: '',
      hospital: '',
      reportDate: this.today(),
      description: ''
    });
  }

  downloadReport(r: MedicalReport): void {
    this.service.downloadReport(r.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = r.originalFileName;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toastService.error('Failed to download report')
    });
  }

  previewReport(r: MedicalReport): void {
    const type = (r.contentType ?? '').toLowerCase();
    const supported = type.startsWith('image/') || type === 'application/pdf';

    if (!supported) {
      this.downloadReport(r);
      return;
    }

    this.service.previewReport(r.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        this.previewObjectUrl = url;
        this.previewSafeUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(url));
        this.previewType.set(type);
        this.previewName.set(r.originalFileName);
        this.showPreview.set(true);
      },
      error: () => this.toastService.error('Failed to preview report')
    });
  }

  closePreview(): void {
    if (this.previewObjectUrl) {
      URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = null;
    }
    this.previewSafeUrl.set(null);
    this.showPreview.set(false);
  }

  isImagePreview(): boolean {
    return this.previewType().startsWith('image/');
  }

  isPdfPreview(): boolean {
    return this.previewType() === 'application/pdf';
  }

  reportIcon(r: MedicalReport): string {
    const type = r.contentType?.toLowerCase() ?? '';
    const ext = r.originalFileName?.split('.').pop()?.toLowerCase() ?? '';
    if (type === 'application/pdf' || ext === 'pdf') return '📄';
    if (type.startsWith('image/') || ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].includes(ext)) return '🖼️';
    if (type.includes('word') || ['doc', 'docx'].includes(ext)) return '📝';
    if (type.startsWith('text/') || ['txt', 'csv'].includes(ext)) return '📃';
    return '📑';
  }

  // ---------- Shared helpers ----------
  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }

  private today(): string {
    return new Date().toISOString().substring(0, 10);
  }
}
