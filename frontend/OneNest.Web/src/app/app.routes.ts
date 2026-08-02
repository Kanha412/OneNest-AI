import { Routes } from '@angular/router';
import { Layout } from './layout/layout';
import { Dashboard } from './features/dashboard/dashboard';
import { Notes } from './features/notes/notes';
import { Tasks } from './features/tasks/tasks';
import { Expenses } from './features/expenses/expenses';
import { Settings } from './features/settings/settings';

export const routes: Routes = [
  {
    path: '',
    component: Layout,
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: Dashboard },
      { path: 'notes', component: Notes },
      { path: 'tasks', component: Tasks },
      { path: 'expenses', component: Expenses },
      { path: 'settings', component: Settings }
    ]
  }
];
