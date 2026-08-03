import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Expense, ExpenseSummary } from '../models/expense.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ExpensesService {

  private readonly http = inject(HttpClient);

  private readonly apiUrl =
    `${environment.apiBaseUrl}/Expenses`;

  getExpenses(): Observable<Expense[]> {
    return this.http.get<Expense[]>(this.apiUrl);
  }

  getSummary(): Observable<ExpenseSummary> {
    return this.http.get<ExpenseSummary>(`${this.apiUrl}/summary`);
  }

  createExpense(expense: Partial<Expense>) {
    return this.http.post<Expense>(this.apiUrl, expense);
  }

  updateExpense(id: string, expense: Partial<Expense>) {
    return this.http.put<Expense>(
      `${this.apiUrl}/${id}`,
      expense
    );
  }

  deleteExpense(id: string) {
    return this.http.delete(
      `${this.apiUrl}/${id}`
    );
  }

}
