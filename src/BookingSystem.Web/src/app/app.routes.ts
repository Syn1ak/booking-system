import { Routes } from '@angular/router';
import { authenticatedGuard } from './core/guards/authenticated.guard';
import { AuthLayoutComponent } from './layout/auth-layout/auth-layout.component';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';

export const routes: Routes = [
  // First, and above the layouts, because both of those are empty-path routes and the auth one
  // would otherwise swallow `/`: it matches, its children are only `login` and `register`, and
  // the router does not backtrack out of a lazy config it has already resolved. The result was
  // the auth shell rendered around an empty outlet, with no redirect and no error.
  { path: '', pathMatch: 'full', redirectTo: 'rooms' },
  {
    path: '',
    component: AuthLayoutComponent,
    loadChildren: () => import('./domains/auth/auth.routes').then((r) => r.ROUTES),
  },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authenticatedGuard],
    children: [
      {
        path: 'rooms',
        loadChildren: () => import('./domains/rooms/rooms.routes').then((r) => r.ROUTES),
      },
      {
        path: 'bookings',
        loadChildren: () => import('./domains/bookings/bookings.routes').then((r) => r.ROUTES),
      },
      {
        path: 'manage/rooms',
        loadChildren: () =>
          import('./domains/rooms/pages/manage-rooms/manage-rooms.routes').then((r) => r.ROUTES),
      },
      {
        path: 'manage/bookings',
        loadChildren: () =>
          import('./domains/bookings/pages/all-bookings/all-bookings.routes').then((r) => r.ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: 'rooms' },
];
