import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { SettingsResponse, UpdateSettingsRequest } from '../models/settings.model';

@Injectable({
  providedIn: 'root'
})
export class SettingsService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/Settings`;

  getSettings(): Observable<SettingsResponse> {
    return this.http.get<SettingsResponse>(this.apiUrl);
  }

  updateSettings(payload: UpdateSettingsRequest): Observable<SettingsResponse> {
    return this.http.put<SettingsResponse>(this.apiUrl, payload);
  }
}
