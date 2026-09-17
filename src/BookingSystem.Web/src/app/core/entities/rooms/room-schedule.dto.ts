export interface IRoomSchedule {
  roomId: string;
  date: string;
  lastBookableDate: string;
  slots: ISlot[];
}

/**
 * `myBookingId` is set only when the caller holds the slot; who holds it otherwise is never sent.
 * `sequence` orders this snapshot against real-time events and is only ever compared.
 */
export interface ISlot {
  slotId: string;
  startsAtUtc: string;
  endsAtUtc: string;
  isBooked: boolean;
  myBookingId: string | null;
  sequence: number;
}
