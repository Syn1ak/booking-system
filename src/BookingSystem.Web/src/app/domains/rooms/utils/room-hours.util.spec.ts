import { IRoom } from '../../../core/entities/rooms/room.dto';
import { slotsPerDay } from './room-hours.util';

describe('slotsPerDay', () => {
  const room = (opensAtUtc: string, closesAtUtc: string, slotLengthMinutes: number): IRoom => ({
    roomId: 'r1',
    name: 'Board room',
    opensAtUtc,
    closesAtUtc,
    slotLengthMinutes,
  });

  it('counts whole slots only, as the server generates them', () => {
    expect(slotsPerDay(room('09:00:00', '17:00:00', 60))).toBe(8);
    expect(slotsPerDay(room('09:00:00', '10:45:00', 30))).toBe(3);
  });
});
