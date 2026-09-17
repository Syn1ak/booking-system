import { AbstractControl, FormGroup } from '@angular/forms';

/** Base for form handler classes: a typed group that also answers template questions. */
export abstract class FormHandler<
  TControls extends Record<string, AbstractControl>,
> extends FormGroup<TControls> {
  isInvalid(name: keyof TControls & string): boolean {
    const control = this.get(name);
    return !!control && control.invalid && control.touched;
  }
}
