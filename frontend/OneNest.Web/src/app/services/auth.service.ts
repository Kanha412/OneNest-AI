import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, LoginRequest, RegisterRequest } from '../models/auth.model';

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

  readonly isAuthenticated = computed(() => this._token() !== null);

  register(request: RegisterRequest) {
    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, request)
      .pipe(tap(response => this.setSession(response)));
  }

  login(request: LoginRequest) {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, request)
      .pipe(tap(response => this.setSession(response)));
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
