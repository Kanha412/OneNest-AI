import { Component, computed, inject } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { HealthService } from '../../services/health.service';
import { NotesService } from '../../services/notes.service';
import { TasksService } from '../../services/tasks.service';
import { ExpensesService } from '../../services/expenses.service';
import {
  ExpenseSummary,
  EXPENSE_CATEGORY_LABELS,
  TransactionType
} from '../../models/expense.model';
import { ChartComponent } from '../../shared/chart/chart';
import { ChartConfiguration } from 'chart.js';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, DecimalPipe, ChartComponent],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class Dashboard {
  private readonly healthService = inject(HealthService);
  private readonly notesService = inject(NotesService);
  private readonly tasksService = inject(TasksService);
  private readonly expensesService = inject(ExpensesService);

  readonly TransactionType = TransactionType;
  readonly categoryLabels = EXPENSE_CATEGORY_LABELS;

  private readonly monthNames = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
  ];

  readonly health = toSignal(this.healthService.getHealth());

  private readonly notes = toSignal(this.notesService.getNotes(), {
    initialValue: []
  });

  private readonly tasks = toSignal(this.tasksService.getTasks(), {
    initialValue: []
  });

  private readonly summary = toSignal<ExpenseSummary | null>(
    this.expensesService.getSummary(),
    { initialValue: null }
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
