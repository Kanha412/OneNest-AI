import {
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';

import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';

import { DatePipe } from '@angular/common';

import { finalize } from 'rxjs';

import { TasksService } from '../../services/tasks.service';
import { TaskItem } from '../../models/task.model';
import { Spinner } from '../../shared/spinner/spinner';
import { ConfirmService } from '../../shared/confirm/confirm.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Paginator } from '../../shared/paginator/paginator';

@Component({
  selector: 'app-tasks',
  imports: [ReactiveFormsModule, DatePipe, Spinner, Paginator],
  templateUrl: './tasks.html',
  styleUrl: './tasks.css'
})

export class Tasks implements OnInit {

  ngOnInit(): void {

    this.loadTasks();

}

  private readonly service =
    inject(TasksService);

  private readonly fb =
    inject(FormBuilder);

  private readonly confirmService =
    inject(ConfirmService);

  private readonly toastService =
    inject(ToastService);

  readonly editingId =
    signal<string | null>(null);

  readonly search =
    signal('');

  readonly currentPage =
    signal(1);

  readonly pageSize = 5;

  readonly today = new Date().toISOString().split('T')[0];

  readonly taskForm = this.fb.group({

    title: ['', Validators.required],

    description: ['', Validators.required],

    dueDate: ['', [Validators.required, this.notPastDate]],

    priority: [2]

  });

  private notPastDate(control: AbstractControl): ValidationErrors | null {

    if (!control.value) {
      return null;
    }

    const selected = new Date(control.value);
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    return selected < today ? { pastDate: true } : null;
  }

  readonly tasks = signal<TaskItem[]>([]);

  readonly isLoading = signal(false);

  readonly isSaving = signal(false);

  readonly filteredTasks =
    computed(() => {

      const text =
        this.search()
          .toLowerCase()
          .trim();

      return this.tasks().filter(task =>
        task.title.toLowerCase().includes(text) ||
        task.description.toLowerCase().includes(text)
      );

    });

  readonly totalPages =
    computed(() =>
      Math.max(1, Math.ceil(this.filteredTasks().length / this.pageSize))
    );

  readonly pagedTasks =
    computed(() => {
      const page = Math.min(this.currentPage(), this.totalPages());
      const start = (page - 1) * this.pageSize;
      return this.filteredTasks().slice(start, start + this.pageSize);
    });

  onSearch(value: string): void {
    this.search.set(value);
    this.currentPage.set(1);
  }

    loadTasks(): void {

  this.isLoading.set(true);

  this.service
      .getTasks()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: tasks => this.tasks.set(tasks),
        error: () => this.toastService.error('Failed to load tasks')
      });

}

saveTask() {

  if (this.taskForm.invalid) {

    this.taskForm.markAllAsTouched();

    return;

  }

  const task = {

      title: this.taskForm.value.title!,

      description: this.taskForm.value.description!,

      dueDate: this.taskForm.value.dueDate,

      priority: Number(this.taskForm.value.priority)

  };

  if (this.editingId()) {

      this.isSaving.set(true);
      this.service.updateTask(
          this.editingId()!,
          task
      )
      .pipe(finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: () => {

          this.editingId.set(null);

          this.taskForm.reset({
              priority:2
          });

          this.loadTasks();

          this.toastService.success('Task updated');

        },
        error: () => this.toastService.error('Failed to update task')
      });

  }
  else {

      this.isSaving.set(true);
      this.service
          .createTask(task)
          .pipe(finalize(() => this.isSaving.set(false)))
          .subscribe({
            next: () => {

              this.taskForm.reset({
                  priority:2
              });

              this.loadTasks();

              this.toastService.success('Task created');

            },
            error: () => this.toastService.error('Failed to create task')
          });

  }

}

editTask(task:TaskItem){

    this.editingId.set(task.id);

    this.taskForm.patchValue({

        title:task.title,

        description:task.description,

        dueDate:task.dueDate,

        priority:task.priority

    });

}

toggleComplete(id:string){

    this.service
        .toggleComplete(id)
        .subscribe({
          next: () => this.loadTasks(),
          error: () => this.toastService.error('Failed to update task')
        });

}

deleteTask(id:string){

    this.confirmService.confirm({
        title: 'Delete task',
        message: 'Are you sure you want to delete this task?',
        confirmText: 'Delete'
    }).then(confirmed => {

        if (!confirmed)
            return;

        this.service.deleteTask(id)
            .subscribe({
              next: () => {

                this.loadTasks();

                this.toastService.success('Task deleted');

              },
              error: () => this.toastService.error('Failed to delete task')
            });

    });

}
}
