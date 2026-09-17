import { FormGroup } from '@angular/forms';

export function getFormControlsNames<TGroup extends FormGroup>(
  form: TGroup,
): { readonly [K in keyof TGroup['controls']]: K } {
  return Object.fromEntries(Object.keys(form.controls).map((name) => [name, name])) as {
    [K in keyof TGroup['controls']]: K;
  };
}
