export interface IBookSlotRequest {
  slotId: string;
}

/** `sequence` is the slot's version as this claim left it. */
export interface IBooking {
  bookingId: string;
  slotId: string;
  roomId: string;
  startsAtUtc: string;
  endsAtUtc: string;
  createdAtUtc: string;
  sequence: number;
}
