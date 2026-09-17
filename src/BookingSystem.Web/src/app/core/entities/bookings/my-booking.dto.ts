export interface IMyBooking {
  bookingId: string;
  slotId: string;
  roomId: string;
  roomName: string;
  startsAtUtc: string;
  endsAtUtc: string;
  createdAtUtc: string;
  cancelledAtUtc: string | null;
}
