import {
  Component,
  computed,
  inject,
  signal
} from '@angular/core';

import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import { toSignal } from '@angular/core/rxjs-interop';

import { DatePipe } from '@angular/common';

import { TasksService } from '../../services/tasks.service';
import { TaskItem } from '../../models/task.model';

@Component({
  selector: 'app-tasks',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './tasks.html',
  styleUrl: './tasks.css'
})

export class Tasks {
  constructor() {

    this.loadTasks();

}

  private readonly service =
    inject(TasksService);

  private readonly fb =
    inject(FormBuilder);

  readonly editingId =
    signal<string | null>(null);

  readonly search =
    signal('');

  readonly taskForm = this.fb.group({

    title: ['', Validators.required],

    description: ['', Validators.required],

    dueDate: [''],

    priority: [2]

  });

  readonly tasks = signal<TaskItem[]>([]);

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

    loadTasks(): void {

  this.service
      .getTasks()
      .subscribe(tasks => {

          this.tasks.set(tasks);

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

      this.service.updateTask(
          this.editingId()!,
          task
      )
      .subscribe(() => {

          this.editingId.set(null);

          this.taskForm.reset({
              priority:2
          });

          this.loadTasks();

      });

  }
  else {

      this.service
          .createTask(task)
          .subscribe(() => {

              this.taskForm.reset({
                  priority:2
              });

              this.loadTasks();

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
        .subscribe(()=>{

            this.loadTasks();

        });

}

deleteTask(id:string){

    if(!confirm("Delete task?"))
        return;

    this.service.deleteTask(id)
        .subscribe(()=>{

            this.loadTasks();

        });

}
}
