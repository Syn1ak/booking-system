/** Times are `TimeOnly` on the server: UTC wall clock, serialised as `HH:mm:ss`. */
export interface IRoom {
  roomId: string;
  name: string;
  opensAtUtc: string;
  closesAtUtc: string;
  slotLengthMinutes: number;
}

export interface IRoomRequest {
  name: string;
  opensAtUtc: string;
  closesAtUtc: string;
  slotLengthMinutes: number;
}
