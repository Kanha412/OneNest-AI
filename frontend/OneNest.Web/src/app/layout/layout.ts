import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Confirm } from '../shared/confirm/confirm';
import { Toast } from '../shared/toast/toast';
import { ConfirmService } from '../shared/confirm/confirm.service';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, Confirm, Toast],
  templateUrl: './layout.html',
  styleUrl: './layout.css'
})
export class Layout {
  private authService = inject(AuthService);
  private router = inject(Router);
  private confirmService = inject(ConfirmService);

  protected readonly currentUser = this.authService.currentUser;

  protected readonly menu = [
    { label: 'Dashboard', path: 'dashboard' },
    { label: 'Notes', path: 'notes' },
    { label: 'Tasks', path: 'tasks' },
    { label: 'Expenses', path: 'expenses' },
    { label: 'Documents', path: 'documents' },
    { label: 'Health', path: 'health' },
    { label: 'Settings', path: 'settings' }
  ];

  async logout(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Logout',
      message: 'Are you sure you want to logout?',
      confirmText: 'Logout',
      cancelText: 'Cancel'
    });

    if (!confirmed) {
      return;
    }

    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
