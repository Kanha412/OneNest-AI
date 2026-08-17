import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast/toast.service';
import { Toast } from '../../shared/toast/toast';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, Toast],
  templateUrl: './register.html',
  styleUrl: './auth.css'
})
export class Register {

  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private toast = inject(ToastService);

  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly registerForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  submit(): void {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.isSubmitting.set(true);

    this.authService.register(this.registerForm.getRawValue())
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => {
          this.toast.success('Account created successfully!');
          this.router.navigate(['/dashboard']);
        },
        error: err => {
          const message = err?.status === 409
            ? (err?.error?.message ?? 'An account with this email already exists.')
            : (err?.error?.message ?? 'Registration failed. Please try again.');
          this.errorMessage.set(message);
          this.toast.error(message);
        }
      });
  }
}
