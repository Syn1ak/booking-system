import { Route } from '@angular/router';
import { capabilityGuard } from '../../../../core/guards/capability.guard';

export const ROUTES: Route[] = [
  {
    path: '',
    title: 'All bookings · RoomBook',
    canActivate: [capabilityGuard('canViewAllBookings')],
    loadComponent: () => import('./all-bookings.component'),
  },
];
