import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Toast } from '../../shared/toast/toast';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, Toast],
  templateUrl: './login.html',
  styleUrl: './auth.css'
})
export class Login {

  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private toast = inject(ToastService);

  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  submit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.isSubmitting.set(true);

    this.authService.login(this.loginForm.getRawValue())
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Welcome back!');
          this.router.navigate(['/dashboard']);
        },
        error: err => {
          const message = err?.error?.message ?? 'Invalid email or password.';
          this.errorMessage.set(message);
          this.toast.error(message);
        }
      });
  }
}
