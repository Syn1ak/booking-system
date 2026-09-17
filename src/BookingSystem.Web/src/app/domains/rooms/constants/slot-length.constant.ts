import { SlotLength } from '../../../core/entities/rooms/slot-length.enum';
import { enumToSelectList } from '../../../core/utils/enum-to-select-list.util';

export const SlotLengthI18Enum: Record<SlotLength, string> = {
  [SlotLength.QuarterHour]: '15 minutes',
  [SlotLength.HalfHour]: '30 minutes',
  [SlotLength.Hour]: '1 hour',
};

export const getSlotLengthI18List = () => enumToSelectList<SlotLength>(SlotLengthI18Enum);
