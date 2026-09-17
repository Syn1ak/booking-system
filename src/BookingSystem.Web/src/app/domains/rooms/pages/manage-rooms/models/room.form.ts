import { AbstractControl, FormControl, ValidationErrors, Validators } from '@angular/forms';
import { NgbTimeStruct } from '@ng-bootstrap/ng-bootstrap';
import { IRoom, IRoomRequest } from '../../../../../core/entities/rooms/room.dto';
import { SlotLength } from '../../../../../core/entities/rooms/slot-length.enum';
import { ControlsOf } from '../../../../../core/utils/form/controls-of.util';
import { FormHandler } from '../../../../../core/utils/form/form-handler.abstraction';
import { getFormControlsNames } from '../../../../../core/utils/form/get-form-controls-names.util';
import { minutesOfDay } from '../../../utils/room-hours.util';

/** Control names match the API's request fields, so server validation errors land on them. */
export type TRoomForm = {
  name: string;
  opensAtUtc: NgbTimeStruct | null;
  closesAtUtc: NgbTimeStruct | null;
  slotLengthMinutes: SlotLength | null;
};

export const NAME_MAX_LENGTH = 200;
export const CLOSES_BEFORE_OPENS = 'closesBeforeOpens';
export const DAY_TOO_SHORT = 'dayTooShort';

const DEFAULT_ROOM: TRoomForm = {
  name: '',
  opensAtUtc: { hour: 9, minute: 0, second: 0 },
  closesAtUtc: { hour: 17, minute: 0, second: 0 },
  slotLengthMinutes: SlotLength.Hour,
};

export class RoomFormHandler extends FormHandler<ControlsOf<TRoomForm>> {
  constructor() {
    super(
      {
        name: new FormControl('', {
          nonNullable: true,
          validators: [Validators.required, Validators.maxLength(NAME_MAX_LENGTH)],
        }),
        opensAtUtc: new FormControl<NgbTimeStruct | null>(null, Validators.required),
        closesAtUtc: new FormControl<NgbTimeStruct | null>(null, Validators.required),
        slotLengthMinutes: new FormControl<SlotLength | null>(null, Validators.required),
      },
      { validators: roomHoursRules },
    );
    this.reset(DEFAULT_ROOM);
  }

  get hoursError(): string | null {
    if (!this.controls.closesAtUtc.touched && !this.controls.opensAtUtc.touched) {
      return null;
    }

    if (this.hasError(CLOSES_BEFORE_OPENS)) {
      return 'Closing time must be after opening time.';
    }

    return this.hasError(DAY_TOO_SHORT)
      ? 'The day must be long enough for at least one slot.'
      : null;
  }

  setFromRoom(room: IRoom | null): void {
    this.reset(
      room
        ? {
            name: room.name,
            opensAtUtc: toTimeStruct(room.opensAtUtc),
            closesAtUtc: toTimeStruct(room.closesAtUtc),
            slotLengthMinutes: room.slotLengthMinutes as SlotLength,
          }
        : DEFAULT_ROOM,
    );
  }

  toRequest(): IRoomRequest {
    const { name, opensAtUtc, closesAtUtc, slotLengthMinutes } = this.getRawValue();

    return {
      name: name.trim(),
      opensAtUtc: toTimeOnly(opensAtUtc as NgbTimeStruct),
      closesAtUtc: toTimeOnly(closesAtUtc as NgbTimeStruct),
      slotLengthMinutes: slotLengthMinutes as SlotLength,
    };
  }

  getFormControlNames() {
    return getFormControlsNames(this);
  }
}

function roomHoursRules(group: AbstractControl): ValidationErrors | null {
  const { opensAtUtc, closesAtUtc, slotLengthMinutes } = group.value as TRoomForm;

  if (!opensAtUtc || !closesAtUtc) {
    return null;
  }

  const length = minutesOfDay(toTimeOnly(closesAtUtc)) - minutesOfDay(toTimeOnly(opensAtUtc));

  if (length <= 0) {
    return { [CLOSES_BEFORE_OPENS]: true };
  }

  return slotLengthMinutes && length < slotLengthMinutes ? { [DAY_TOO_SHORT]: true } : null;
}

function toTimeStruct(timeOnly: string): NgbTimeStruct {
  const [hour, minute] = timeOnly.split(':').map(Number);
  return { hour, minute, second: 0 };
}

function toTimeOnly({ hour, minute }: NgbTimeStruct): string {
  return `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00`;
}
