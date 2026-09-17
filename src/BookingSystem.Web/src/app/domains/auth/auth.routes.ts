import { Route } from '@angular/router';
import { anonymousGuard } from '../../core/guards/anonymous.guard';

export const ROUTES: Route[] = [
  {
    path: 'login',
    title: 'Sign in · RoomBook',
    canActivate: [anonymousGuard],
    loadComponent: () => import('./pages/login/login.component'),
  },
  {
    path: 'register',
    title: 'Create account · RoomBook',
    canActivate: [anonymousGuard],
    loadComponent: () => import('./pages/register/register.component'),
  },
];
