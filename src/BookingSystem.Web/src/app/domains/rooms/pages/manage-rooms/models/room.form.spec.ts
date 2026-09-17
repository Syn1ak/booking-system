import { SlotLength } from '../../../../../core/entities/rooms/slot-length.enum';
import { CLOSES_BEFORE_OPENS, DAY_TOO_SHORT, RoomFormHandler } from './room.form';

describe('RoomFormHandler', () => {
  it('round-trips a room into the request the API expects', () => {
    const form = new RoomFormHandler();

    form.setFromRoom({
      roomId: 'r1',
      name: ' Board room ',
      opensAtUtc: '08:30:00',
      closesAtUtc: '17:00:00',
      slotLengthMinutes: 30,
    });

    expect(form.valid).toBe(true);
    expect(form.toRequest()).toEqual({
      name: 'Board room',
      opensAtUtc: '08:30:00',
      closesAtUtc: '17:00:00',
      slotLengthMinutes: 30,
    });
  });

  it('refuses a closing time that is not after the opening time', () => {
    const form = new RoomFormHandler();

    form.patchValue({
      opensAtUtc: { hour: 17, minute: 0, second: 0 },
      closesAtUtc: { hour: 9, minute: 0, second: 0 },
    });

    expect(form.hasError(CLOSES_BEFORE_OPENS)).toBe(true);
  });

  it('refuses a day too short for one slot, as the server does', () => {
    const form = new RoomFormHandler();

    form.patchValue({
      opensAtUtc: { hour: 9, minute: 0, second: 0 },
      closesAtUtc: { hour: 9, minute: 45, second: 0 },
      slotLengthMinutes: SlotLength.Hour,
    });

    expect(form.hasError(DAY_TOO_SHORT)).toBe(true);
  });
});
