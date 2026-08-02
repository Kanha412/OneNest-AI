import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
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
