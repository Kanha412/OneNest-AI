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

import { DatePipe, DecimalPipe } from '@angular/common';

import { finalize } from 'rxjs';

import { ExpensesService } from '../../services/expenses.service';
import {
  Expense,
  ExpenseCategory,
  TransactionType,
  EXPENSE_CATEGORY_LABELS
} from '../../models/expense.model';
import { Spinner } from '../../shared/spinner/spinner';
import { ConfirmService } from '../../shared/confirm/confirm.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Paginator } from '../../shared/paginator/paginator';

type SortOption = 'latest' | 'oldest' | 'highest' | 'lowest';

@Component({
  selector: 'app-expenses',
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, Spinner, Paginator],
  templateUrl: './expenses.html',
  styleUrl: './expenses.css'
})
export class Expenses implements OnInit {

  ngOnInit(): void {
    this.loadExpenses();
  }

  private readonly service = inject(ExpensesService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastService = inject(ToastService);

  readonly categoryLabels = EXPENSE_CATEGORY_LABELS;

  readonly categories = [
    ExpenseCategory.Food,
    ExpenseCategory.Travel,
    ExpenseCategory.Shopping,
    ExpenseCategory.Bills,
    ExpenseCategory.Entertainment,
    ExpenseCategory.Health,
    ExpenseCategory.Education,
    ExpenseCategory.Salary,
    ExpenseCategory.Investment,
    ExpenseCategory.Other
  ];

  readonly TransactionType = TransactionType;

  readonly editingId = signal<string | null>(null);
  readonly search = signal('');
  readonly categoryFilter = signal<number | 'all'>('all');
  readonly typeFilter = signal<number | 'all'>('all');
  readonly sortBy = signal<SortOption>('latest');
  readonly currentPage = signal(1);
  readonly pageSize = 5;

  readonly today = new Date().toISOString().split('T')[0];

  readonly expenses = signal<Expense[]>([]);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);

  readonly expenseForm = this.fb.group({
    title: ['', Validators.required],
    amount: [null as number | null, [Validators.required, Validators.min(0.01)]],
    category: [ExpenseCategory.Food],
    transactionType: [TransactionType.Expense],
    date: ['', Validators.required],
    notes: ['']
  });

  readonly filteredExpenses = computed(() => {
    const text = this.search().toLowerCase().trim();
    const category = this.categoryFilter();
    const type = this.typeFilter();
    const sort = this.sortBy();

    let result = this.expenses().filter(expense => {
      const matchesText =
        expense.title.toLowerCase().includes(text) ||
        (expense.notes ?? '').toLowerCase().includes(text);

      const matchesCategory =
        category === 'all' || expense.category === category;

      const matchesType =
        type === 'all' || expense.transactionType === type;

      return matchesText && matchesCategory && matchesType;
    });

    result = [...result].sort((a, b) => {
      switch (sort) {
        case 'oldest':
          return new Date(a.date).getTime() - new Date(b.date).getTime();
        case 'highest':
          return b.amount - a.amount;
        case 'lowest':
          return a.amount - b.amount;
        case 'latest':
        default:
          return new Date(b.date).getTime() - new Date(a.date).getTime();
      }
    });

    return result;
  });

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredExpenses().length / this.pageSize))
  );

  readonly pagedExpenses = computed(() => {
    const page = Math.min(this.currentPage(), this.totalPages());
    const start = (page - 1) * this.pageSize;
    return this.filteredExpenses().slice(start, start + this.pageSize);
  });

  onSearch(value: string): void {
    this.search.set(value);
    this.currentPage.set(1);
  }

  onCategoryFilter(value: string): void {
    this.categoryFilter.set(value === 'all' ? 'all' : Number(value));
    this.currentPage.set(1);
  }

  onTypeFilter(value: string): void {
    this.typeFilter.set(value === 'all' ? 'all' : Number(value));
    this.currentPage.set(1);
  }

  onSort(value: string): void {
    this.sortBy.set(value as SortOption);
    this.currentPage.set(1);
  }

  loadExpenses(): void {
    this.isLoading.set(true);

    this.service
      .getExpenses()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: expenses => this.expenses.set(expenses),
        error: () => this.toastService.error('Failed to load expenses')
      });
  }

  saveExpense(): void {
    if (this.expenseForm.invalid) {
      this.expenseForm.markAllAsTouched();
      return;
    }

    const expense = {
      title: this.expenseForm.value.title!,
      amount: Number(this.expenseForm.value.amount),
      category: Number(this.expenseForm.value.category),
      transactionType: Number(this.expenseForm.value.transactionType),
      date: this.expenseForm.value.date!,
      notes: this.expenseForm.value.notes ?? ''
    };

    if (this.editingId()) {
      this.isSaving.set(true);
      this.service
        .updateExpense(this.editingId()!, expense)
        .pipe(finalize(() => this.isSaving.set(false)))
        .subscribe({
          next: () => {
            this.resetForm();
            this.loadExpenses();
            this.toastService.success('Expense updated');
          },
          error: () => this.toastService.error('Failed to update expense')
        });
    } else {
      this.isSaving.set(true);
      this.service
        .createExpense(expense)
        .pipe(finalize(() => this.isSaving.set(false)))
        .subscribe({
          next: () => {
            this.resetForm();
            this.loadExpenses();
            this.toastService.success('Expense created');
          },
          error: () => this.toastService.error('Failed to create expense')
        });
    }
  }

  editExpense(expense: Expense): void {
    this.editingId.set(expense.id);

    this.expenseForm.patchValue({
      title: expense.title,
      amount: expense.amount,
      category: expense.category,
      transactionType: expense.transactionType,
      date: expense.date ? expense.date.split('T')[0] : '',
      notes: expense.notes
    });
  }

  cancelEdit(): void {
    this.resetForm();
  }

  deleteExpense(id: string): void {
    this.confirmService.confirm({
      title: 'Delete expense',
      message: 'Are you sure you want to delete this expense?',
      confirmText: 'Delete'
    }).then(confirmed => {
      if (!confirmed) {
        return;
      }

      this.service.deleteExpense(id)
        .subscribe({
          next: () => {
            this.loadExpenses();
            this.toastService.success('Expense deleted');
          },
          error: () => this.toastService.error('Failed to delete expense')
        });
    });
  }

  private resetForm(): void {
    this.editingId.set(null);
    this.expenseForm.reset({
      category: ExpenseCategory.Food,
      transactionType: TransactionType.Expense,
      title: '',
      amount: null,
      date: '',
      notes: ''
    });
  }
}
