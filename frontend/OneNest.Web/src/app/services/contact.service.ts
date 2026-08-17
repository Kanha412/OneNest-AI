import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ContactMessage, CreateContactRequest } from '../models/contact.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ContactService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/Contact`;

  create(request: CreateContactRequest): Observable<ContactMessage> {
    return this.http.post<ContactMessage>(this.apiUrl, request);
  }

  getMyMessages(): Observable<ContactMessage[]> {
    return this.http.get<ContactMessage[]>(`${this.apiUrl}/my`);
  }
}
