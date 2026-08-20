import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AuthResponse,
  ChangePasswordRequest,
  DeleteAccountRequest,
  LoginRequest,
  RegisterRequest
} from '../models/auth.model';

const TOKEN_KEY = 'onenest.token';
const USER_KEY = 'onenest.user';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private http = inject(HttpClient);

  private readonly apiUrl = `${environment.apiBaseUrl}/Auth`;

  private readonly _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));

  private readonly _user = signal<AuthResponse | null>(this.readUser());

  readonly currentUser = this._user.asReadonly();

  /**
   * Returns true only when a token exists AND has not expired.
   *
   * WHY: previously this only checked token !== null.  An expired JWT stored
   * in localStorage passed the guard, Angular instantiated the Dashboard,
   * toSignal() fired API calls, the backend returned 401, and the catchError
   * handler showed a "Failed to load dashboard" toast before the
   * unauthorizedInterceptor could redirect to /login.
   *
   * By decoding the JWT's `exp` claim here (no library needed — just
   * atob + JSON.parse), an expired token is treated as absent: the guard
   * redirects to /login immediately, Dashboard is never created, and no
   * spurious toast is shown.
   */
  readonly isAuthenticated = computed(() => {
    const token = this._token();
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      // exp is a Unix timestamp (seconds); Date.now() is milliseconds
      return typeof payload.exp === 'number' && payload.exp * 1000 > Date.now();
    } catch {
      // Malformed token — treat as unauthenticated
      return false;
    }
  });

  register(request: RegisterRequest) {
    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, request)
      .pipe(tap(response => this.setSession(response)));
  }

  login(request: LoginRequest) {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, request)
      .pipe(tap(response => this.setSession(response)));
  }

  changePassword(request: ChangePasswordRequest) {
    return this.http.post<{ message: string }>(`${this.apiUrl}/change-password`, request);
  }

  deleteAccount(request: DeleteAccountRequest) {
    return this.http.post<{ message: string }>(`${this.apiUrl}/delete-account`, request);
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._token.set(null);
    this._user.set(null);
  }

  getToken(): string | null {
    return this._token();
  }

  updateCurrentUserProfile(fullName: string, email: string): void {
    const current = this._user();
    if (!current) {
      return;
    }

    const updated: AuthResponse = {
      ...current,
      fullName,
      email
    };

    localStorage.setItem(USER_KEY, JSON.stringify(updated));
    this._user.set(updated);
  }

  private setSession(response: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USER_KEY, JSON.stringify(response));
    this._token.set(response.token);
    this._user.set(response);
  }

  private readUser(): AuthResponse | null {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) as AuthResponse : null;
  }
}
