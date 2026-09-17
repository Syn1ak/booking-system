import { AbstractControl, FormControl, ValidationErrors } from '@angular/forms';
import { IAdminBookingsCriteria } from '../../../../../core/entities/bookings/admin-bookings.criteria';
import { ControlsOf } from '../../../../../core/utils/form/controls-of.util';
import { FormHandler } from '../../../../../core/utils/form/form-handler';
import { getFormControlsNames } from '../../../../../core/utils/form/get-form-controls-names.util';

export type TBookingsFilters = {
  roomId: string | null;
  from: string | null;
  to: string | null;
  includeCancelled: boolean;
};

/** Query parameters arrive as strings, or not at all. */
export type TBookingsFiltersParams = Partial<Record<keyof TBookingsFilters, string | null>>;

export const RANGE_REVERSED = 'rangeReversed';

export class BookingsFiltersFormHandler extends FormHandler<ControlsOf<TBookingsFilters>> {
  constructor() {
    super(
      {
        roomId: new FormControl<string | null>(null),
        from: new FormControl<string | null>(null),
        to: new FormControl<string | null>(null),
        includeCancelled: new FormControl(true, { nonNullable: true }),
      },
      { validators: rangeInOrder },
    );
  }

  static toCriteria(params: TBookingsFiltersParams): IAdminBookingsCriteria {
    return {
      roomId: params.roomId || null,
      from: params.from || null,
      to: params.to || null,
      includeCancelled: params.includeCancelled !== 'false',
    };
  }

  setFromCriteria(criteria: IAdminBookingsCriteria): void {
    this.reset(
      {
        roomId: criteria.roomId ?? null,
        from: criteria.from ?? null,
        to: criteria.to ?? null,
        includeCancelled: criteria.includeCancelled ?? true,
      },
      { emitEvent: false },
    );
  }

  toQueryParams(): TBookingsFiltersParams {
    const { roomId, from, to, includeCancelled } = this.getRawValue();

    return {
      roomId: roomId || null,
      from: from || null,
      to: to || null,
      includeCancelled: includeCancelled ? null : 'false',
    };
  }

  getFormControlNames() {
    return getFormControlsNames(this);
  }
}

function rangeInOrder(group: AbstractControl): ValidationErrors | null {
  const { from, to } = group.value as TBookingsFilters;
  return from && to && from > to ? { [RANGE_REVERSED]: true } : null;
}
