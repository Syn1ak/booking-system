import { FormControl, FormGroup } from '@angular/forms';
import { applyValidationErrors, SERVER_ERROR_KEY } from './apply-validation-errors.util';

describe('applyValidationErrors', () => {
  const createForm = () =>
    new FormGroup({ name: new FormControl(''), closesAtUtc: new FormControl('') });

  it('puts a PascalCase server field on its camelCase control', () => {
    const form = createForm();

    applyValidationErrors(form)({
      status: 400,
      errors: { ClosesAtUtc: ['Must be after opening.'] },
    });

    const control = form.controls.closesAtUtc;
    expect(control.getError(SERVER_ERROR_KEY)).toBe('Must be after opening.');
    expect(control.touched).toBe(true);
  });

  it('returns messages that name no control', () => {
    const form = createForm();

    const unmatched = applyValidationErrors(form)({
      status: 400,
      errors: { PasswordTooShort: ['Passwords must be at least 8 characters.'] },
    });

    expect(unmatched).toEqual(['Passwords must be at least 8 characters.']);
  });
});
