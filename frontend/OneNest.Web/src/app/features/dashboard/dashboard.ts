import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { NotesService } from '../../services/notes.service';
import { TasksService } from '../../services/tasks.service';
import { ExpensesService } from '../../services/expenses.service';
import { DocumentsService } from '../../services/documents.service';
import { HealthHubService } from '../../services/health-hub.service';
import { ToastService } from '../../shared/toast/toast.service';
import {
  ExpenseSummary,
  EXPENSE_CATEGORY_LABELS,
  TransactionType
} from '../../models/expense.model';
import {
  DocumentSummary,
  DOCUMENT_CATEGORY_LABELS
} from '../../models/document.model';
import { HealthSummary } from '../../models/health.model';
import { ChartComponent } from '../../shared/chart/chart';
import { Spinner } from '../../shared/spinner/spinner';
import { ChartConfiguration } from 'chart.js';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, DecimalPipe, ChartComponent, Spinner],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class Dashboard {
  private readonly authService = inject(AuthService);
  private readonly notesService = inject(NotesService);
  private readonly tasksService = inject(TasksService);
  private readonly expensesService = inject(ExpensesService);
  private readonly documentsService = inject(DocumentsService);
  private readonly healthHubService = inject(HealthHubService);
  private readonly toastService = inject(ToastService);

  /** First name extracted from the logged-in user's full name. */
  protected readonly firstName = computed(() => {
    const name = this.authService.currentUser()?.fullName ?? '';
    return name.split(' ')[0] || name;
  });

  /** Time-of-day greeting evaluated once on component creation. */
  protected readonly greeting = (() => {
    const h = new Date().getHours();
    if (h < 12) return 'Good morning';
    if (h < 17) return 'Good afternoon';
    return 'Good evening';
  })();

  /** Today's date formatted for the header, e.g. "Tue, 18 Aug 2026". */
  protected readonly todayLabel = new Intl.DateTimeFormat('en-IN', {
    weekday: 'short', day: 'numeric', month: 'short', year: 'numeric'
  }).format(new Date());

  readonly TransactionType = TransactionType;
  readonly categoryLabels = EXPENSE_CATEGORY_LABELS;
  readonly documentCategoryLabels = DOCUMENT_CATEGORY_LABELS;

  private readonly monthNames = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
  ];

  // Track whether any critical API call failed (prevents infinite spinner)
  private readonly _hasLoadError = signal(false);
  readonly hasLoadError = this._hasLoadError.asReadonly();

  private readonly notes = toSignal(
    this.notesService.getNotes().pipe(catchError(() => of([]))),
    { initialValue: [] }
  );

  private readonly tasks = toSignal(
    this.tasksService.getTasks().pipe(catchError(() => of([]))),
    { initialValue: [] }
  );

  private readonly summary = toSignal<ExpenseSummary | null>(
    this.expensesService.getSummary().pipe(
      catchError(() => {
        if (!this._hasLoadError()) {
          this._hasLoadError.set(true);
          this.toastService.error('Failed to load dashboard data. Please refresh.');
        }
        return of(null);
      })
    ),
    { initialValue: null }
  );

  private readonly documentSummary = toSignal<DocumentSummary | null>(
    this.documentsService.getSummary().pipe(
      catchError(() => {
        if (!this._hasLoadError()) {
          this._hasLoadError.set(true);
          this.toastService.error('Failed to load dashboard data. Please refresh.');
        }
        return of(null);
      })
    ),
    { initialValue: null }
  );

  private readonly healthSummary = toSignal<HealthSummary | null>(
    this.healthHubService.getSummary().pipe(
      catchError(() => {
        if (!this._hasLoadError()) {
          this._hasLoadError.set(true);
          this.toastService.error('Failed to load dashboard data. Please refresh.');
        }
        return of(null);
      })
    ),
    { initialValue: null }
  );

  readonly isLoading = computed(() =>
    !this._hasLoadError() && (
      this.summary() === null ||
      this.documentSummary() === null ||
      this.healthSummary() === null
    )
  );

  readonly totalNotes = computed(() => this.notes().length);
  readonly pinnedNotes = computed(() =>
    this.notes().filter(note => note.isPinned).length
  );

  readonly totalTasks = computed(() => this.tasks().length);
  readonly completedTasks = computed(() =>
    this.tasks().filter(task => task.isCompleted).length
  );
  readonly pendingTasks = computed(() =>
    this.tasks().filter(task => !task.isCompleted).length
  );
  readonly overdueTasks = computed(() => {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    return this.tasks().filter(task =>
      !task.isCompleted &&
      task.dueDate !== null &&
      new Date(task.dueDate) < today
    ).length;
  });

  readonly finance = this.summary;

  readonly documents = this.documentSummary;

  readonly totalDocuments = computed(() => this.documentSummary()?.totalDocuments ?? 0);

  readonly storageUsedLabel = computed(() => {
    const bytes = this.documentSummary()?.storageUsed ?? 0;
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  });

  readonly todayUploads = computed(() =>
    this.documentSummary()?.todayUploads ?? 0
  );

  readonly healthHub = this.healthSummary;

  readonly activeMedicines = computed(() => this.healthSummary()?.activeMedicines ?? 0);
  readonly upcomingAppointments = computed(() => this.healthSummary()?.upcomingAppointments ?? 0);
  readonly totalReports = computed(() => this.healthSummary()?.totalReports ?? 0);
  readonly lastRecordUpdate = computed(() => this.healthSummary()?.lastRecordUpdate ?? null);
  readonly recentReports = computed(() => this.healthSummary()?.recentReports ?? []);
  readonly expiringSoonMedicines = computed(() => this.healthSummary()?.expiringSoonMedicines ?? 0);
  readonly upcomingAppointmentsList = computed(() => this.healthSummary()?.upcomingAppointmentsList ?? []);

  readonly medicineDistributionChartData = computed<ChartConfiguration['data']>(() => {
    const distribution = this.healthSummary()?.medicineDistribution ?? [];
    return {
      labels: distribution.map(item => item.timing),
      datasets: [
        {
          data: distribution.map(item => item.count),
          backgroundColor: ['#f59e0b', '#0ea5e9', '#4f46e5']
        }
      ]
    };
  });

  readonly appointmentTimelineChartData = computed<ChartConfiguration['data']>(() => {
    const timeline = this.healthSummary()?.appointmentTimeline ?? [];
    return {
      labels: timeline.map(item => `${this.monthNames[item.month - 1]} ${item.year}`),
      datasets: [
        {
          label: 'Appointments',
          data: timeline.map(item => item.count),
          backgroundColor: '#4f46e5'
        }
      ]
    };
  });

  documentIcon(fileName: string, contentType: string): string {
    const type = contentType?.toLowerCase() ?? '';
    const ext = fileName?.split('.').pop()?.toLowerCase() ?? '';

    if (type === 'application/pdf' || ext === 'pdf') return '📄';
    if (type.includes('word') || ext === 'doc' || ext === 'docx') return '📝';
    if (type.includes('sheet') || type.includes('excel') || ['xls', 'xlsx', 'csv'].includes(ext)) return '📊';
    if (type.includes('presentation') || type.includes('powerpoint') || ['ppt', 'pptx'].includes(ext)) return '📽️';
    if (type.startsWith('image/') || ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp', 'svg'].includes(ext)) return '🖼️';
    if (['zip', 'rar', '7z', 'tar', 'gz'].includes(ext)) return '🗜️';
    if (type.startsWith('text/') || ['txt', 'rtf'].includes(ext)) return '📃';
    return '📑';
  }

  readonly documentCategoryChartData = computed<ChartConfiguration['data']>(() => {
    const distribution = this.documentSummary()?.categoryDistribution ?? [];
    return {
      labels: distribution.map(item => this.documentCategoryLabels[item.category]),
      datasets: [
        {
          data: distribution.map(item => item.count),
          backgroundColor: [
            '#4f46e5', '#0ea5e9', '#f59e0b', '#ef4444', '#10b981',
            '#8b5cf6', '#ec4899', '#22c55e', '#64748b'
          ]
        }
      ]
    };
  });

  readonly topCategoryLabel = computed(() => {
    const category = this.summary()?.topExpenseCategory;
    return category ? this.categoryLabels[category] : '—';
  });

  readonly categoryChartData = computed<ChartConfiguration['data']>(() => {
    const breakdown = this.summary()?.categoryBreakdown ?? [];
    return {
      labels: breakdown.map(item => this.categoryLabels[item.category]),
      datasets: [
        {
          data: breakdown.map(item => item.totalAmount),
          backgroundColor: [
            '#4f46e5', '#0ea5e9', '#f59e0b', '#ef4444', '#10b981',
            '#8b5cf6', '#ec4899', '#22c55e', '#eab308', '#64748b'
          ]
        }
      ]
    };
  });

  readonly monthlyChartData = computed<ChartConfiguration['data']>(() => {
    const breakdown = this.summary()?.monthlyBreakdown ?? [];
    return {
      labels: breakdown.map(item => `${this.monthNames[item.month - 1]} ${item.year}`),
      datasets: [
        {
          label: 'Income',
          data: breakdown.map(item => item.income),
          backgroundColor: '#10b981'
        },
        {
          label: 'Expense',
          data: breakdown.map(item => item.expense),
          backgroundColor: '#ef4444'
        }
      ]
    };
  });
}
