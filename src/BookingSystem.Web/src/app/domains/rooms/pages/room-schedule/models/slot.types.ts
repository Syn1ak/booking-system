import { ISlot } from '../../../../../core/entities/rooms/room-schedule.dto';

export type TSlotStatus = 'free' | 'booked' | 'mine' | 'past';

export type TSlotChangeResult =
  { kind: 'ignored' } | { kind: 'applied'; slots: ISlot[] } | { kind: 'unreconcilable' };
