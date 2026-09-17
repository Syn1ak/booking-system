import { FormGroup } from '@angular/forms';
import { IValidationProblemDetails } from '../../errors/problem-details';

export const SERVER_ERROR_KEY = 'server';

/**
 * Puts each server message on the control it names, as `{ server: message }`. Returns the
 * messages that name no control — Identity reports password rules under codes such as
 * `PasswordTooShort` — so the caller can show them beside the submit button.
 */
export function applyValidationErrors(form: FormGroup) {
  return (problem: IValidationProblemDetails): string[] => {
    const unmatched: string[] = [];

    for (const [field, messages] of Object.entries(problem.errors)) {
      const control = form.get(field.charAt(0).toLowerCase() + field.slice(1));

      if (control) {
        control.setErrors({ ...control.errors, [SERVER_ERROR_KEY]: messages[0] });
        control.markAsTouched();
      } else {
        unmatched.push(...messages);
      }
    }

    return unmatched;
  };
}
