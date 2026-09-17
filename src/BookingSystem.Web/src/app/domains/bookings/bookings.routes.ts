import { Route } from '@angular/router';

export const ROUTES: Route[] = [
  {
    path: '',
    title: 'My bookings · RoomBook',
    loadComponent: () => import('./pages/my-bookings/my-bookings.component'),
  },
];
