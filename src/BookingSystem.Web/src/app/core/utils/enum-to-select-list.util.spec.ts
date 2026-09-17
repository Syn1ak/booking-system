import { SlotLength } from '../entities/rooms/slot-length.enum';
import { enumToSelectList } from './enum-to-select-list.util';

describe('enumToSelectList', () => {
  it('keeps numeric enum values as numbers', () => {
    expect(
      enumToSelectList<SlotLength>({
        [SlotLength.QuarterHour]: '15 minutes',
        [SlotLength.HalfHour]: '30 minutes',
        [SlotLength.Hour]: '1 hour',
      }),
    ).toEqual([
      { value: 15, label: '15 minutes' },
      { value: 30, label: '30 minutes' },
      { value: 60, label: '1 hour' },
    ]);
  });
});
