import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError(error => {
      if (error?.status === 401) {
        // Always clear the session on a server-side 401 — the token may be
        // expired, revoked, or malformed.  logout() is idempotent so calling
        // it when already logged out is harmless.  Navigate only when a token
        // was actually present so a truly-anonymous request (e.g. register)
        // that returns 401 doesn't redirect unnecessarily.
        if (authService.getToken()) {
          authService.logout();
          router.navigate(['/login']);
        }
      }
      return throwError(() => error);
    })
  );
};
