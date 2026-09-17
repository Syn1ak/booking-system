import { Route } from '@angular/router';

export const ROUTES: Route[] = [
  {
    path: '',
    title: 'Rooms · RoomBook',
    loadComponent: () => import('./pages/room-list/room-list.component'),
  },
];
