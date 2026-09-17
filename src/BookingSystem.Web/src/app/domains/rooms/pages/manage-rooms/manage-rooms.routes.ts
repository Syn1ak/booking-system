import { Route } from '@angular/router';
import { capabilityGuard } from '../../../../core/guards/capability.guard';

export const ROUTES: Route[] = [
  {
    path: '',
    title: 'Manage rooms · RoomBook',
    canActivate: [capabilityGuard('canManageRooms')],
    loadComponent: () => import('./manage-rooms.component'),
  },
];
