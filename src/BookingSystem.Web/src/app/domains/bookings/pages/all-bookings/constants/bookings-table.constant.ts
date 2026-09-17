export const PAGE_SIZE = 12;

export type TBookingsSortKey = 'startsAtUtc' | 'roomName' | 'userEmail' | 'createdAtUtc';

export const BOOKINGS_COLUMNS: readonly { key: TBookingsSortKey; label: string }[] = [
  { key: 'startsAtUtc', label: 'When' },
  { key: 'roomName', label: 'Room' },
  { key: 'userEmail', label: 'Booked by' },
  { key: 'createdAtUtc', label: 'Booked at' },
];
