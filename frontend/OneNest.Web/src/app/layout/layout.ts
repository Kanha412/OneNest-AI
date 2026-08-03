import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Confirm } from '../shared/confirm/confirm';
import { Toast } from '../shared/toast/toast';

@Component({
  selector: 'app-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, Confirm, Toast],
  templateUrl: './layout.html',
  styleUrl: './layout.css'
})
export class Layout {
  protected readonly menu = [
    { label: 'Dashboard', path: 'dashboard' },
    { label: 'Notes', path: 'notes' },
    { label: 'Tasks', path: 'tasks' },
    { label: 'Expenses', path: 'expenses' },
    { label: 'Settings', path: 'settings' }
  ];
}
