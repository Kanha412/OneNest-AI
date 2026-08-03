import { Routes } from '@angular/router';
import { Layout } from './layout/layout';
import { Dashboard } from './features/dashboard/dashboard';
import { Notes } from './features/notes/notes';
import { Tasks } from './features/tasks/tasks';
import { Expenses } from './features/expenses/expenses';
import { Settings } from './features/settings/settings';
import { Login } from './features/auth/login';
import { Register } from './features/auth/register';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'register', component: Register },
  {
    path: '',
    component: Layout,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: Dashboard },
      { path: 'notes', component: Notes },
      { path: 'tasks', component: Tasks },
      { path: 'expenses', component: Expenses },
      { path: 'settings', component: Settings }
    ]
  },
  { path: '**', redirectTo: '' }
];
