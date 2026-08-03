import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TaskItem } from '../models/task.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class TasksService {

  private readonly http = inject(HttpClient);

  private readonly apiUrl =
    `${environment.apiBaseUrl}/Tasks`;

  getTasks(): Observable<TaskItem[]> {
    return this.http.get<TaskItem[]>(this.apiUrl);
  }

  createTask(task: Partial<TaskItem>) {
    return this.http.post<TaskItem>(this.apiUrl, task);
  }

  updateTask(id: string, task: Partial<TaskItem>) {
    return this.http.put<TaskItem>(
      `${this.apiUrl}/${id}`,
      task
    );
  }

  deleteTask(id: string) {
    return this.http.delete(
      `${this.apiUrl}/${id}`
    );
  }

  toggleComplete(id: string) {
    return this.http.patch(
      `${this.apiUrl}/${id}/complete`,
      {}
    );
  }

}
