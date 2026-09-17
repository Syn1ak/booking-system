import { IRoom } from '../../../core/entities/rooms/room.dto';

export function minutesOfDay(timeOnly: string): number {
  const [hours, minutes] = timeOnly.split(':').map(Number);
  return hours * 60 + minutes;
}

export function slotsPerDay({ opensAtUtc, closesAtUtc, slotLengthMinutes }: IRoom): number {
  return Math.floor((minutesOfDay(closesAtUtc) - minutesOfDay(opensAtUtc)) / slotLengthMinutes);
}
