import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ContactMessage, UpdateContactStatusRequest } from '../models/contact.model';
import { AdminUser, UpdateUserRoleRequest } from '../models/admin.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/Admin`;

  // Messages
  getAllMessages(): Observable<ContactMessage[]> {
    return this.http.get<ContactMessage[]>(`${this.apiUrl}/messages`);
  }

  updateMessageStatus(id: string, request: UpdateContactStatusRequest): Observable<ContactMessage> {
    return this.http.patch<ContactMessage>(`${this.apiUrl}/messages/${id}/status`, request);
  }

  // Users
  getAllUsers(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(`${this.apiUrl}/users`);
  }

  updateUserRole(id: string, request: UpdateUserRoleRequest): Observable<AdminUser> {
    return this.http.patch<AdminUser>(`${this.apiUrl}/users/${id}/role`, request);
  }
}
