import { Routes } from '@angular/router';
import { authenticatedGuard } from './core/guards/authenticated.guard';
import { AuthLayoutComponent } from './layout/auth-layout/auth-layout.component';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';

export const routes: Routes = [
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
      { path: '', pathMatch: 'full', redirectTo: 'rooms' },
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
