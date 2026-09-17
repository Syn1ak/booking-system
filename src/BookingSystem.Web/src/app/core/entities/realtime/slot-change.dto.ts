/** Deliberately carries no holder: one payload reaches every viewer of the room. */
export interface ISlotChange {
  roomId: string;
  slotId: string;
  startsAtUtc: string;
  isBooked: boolean;
  sequence: number;
}
