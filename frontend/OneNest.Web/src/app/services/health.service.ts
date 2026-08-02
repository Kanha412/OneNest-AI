import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { HealthResponse } from '../models/health.model';

@Injectable({
  providedIn: 'root'
})
export class HealthService {

  private readonly http = inject(HttpClient);

  private readonly baseUrl = 'http://localhost:5189/api/health';

  getHealth(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>(this.baseUrl);
  }
}