import { TNavLink } from '../models/nav-link.types';

export const NAV_LINKS: readonly TNavLink[] = [
  { path: '/rooms', label: 'Rooms', icon: 'door-open' },
  { path: '/bookings', label: 'My bookings', icon: 'calendar2-check' },
  { path: '/manage/rooms', label: 'Manage rooms', icon: 'sliders', capability: 'canManageRooms' },
  {
    path: '/manage/bookings',
    label: 'All bookings',
    icon: 'journal-text',
    capability: 'canViewAllBookings',
  },
];
