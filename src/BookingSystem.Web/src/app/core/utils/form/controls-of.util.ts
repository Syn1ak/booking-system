import { FormControl, FormGroup } from '@angular/forms';

type TPrimitive = string | number | boolean | null | undefined | Date;

export type ControlsOf<T extends object> = {
  [K in keyof T]: T[K] extends TPrimitive | readonly unknown[]
    ? FormControl<T[K]>
    : T[K] extends object
      ? FormGroup<ControlsOf<T[K]>> | FormControl<T[K]>
      : FormControl<T[K]>;
};
