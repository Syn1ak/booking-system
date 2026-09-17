import { AbstractControl, FormControl, ValidationErrors, Validators } from '@angular/forms';
import { IRegisterRequest } from '../../../../../core/entities/auth/register.dto';
import { ControlsOf } from '../../../../../core/utils/form/controls-of.util';
import { FormHandler } from '../../../../../core/utils/form/form-handler';
import { getFormControlsNames } from '../../../../../core/utils/form/get-form-controls-names.util';

export type TRegisterForm = {
  email: string;
  password: string;
  confirmPassword: string;
};

export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MISMATCH = 'passwordMismatch';

export class RegisterFormHandler extends FormHandler<ControlsOf<TRegisterForm>> {
  constructor() {
    super(
      {
        email: new FormControl('', {
          nonNullable: true,
          validators: [Validators.required, Validators.email],
        }),
        password: new FormControl('', {
          nonNullable: true,
          validators: [Validators.required, Validators.minLength(PASSWORD_MIN_LENGTH)],
        }),
        confirmPassword: new FormControl('', {
          nonNullable: true,
          validators: [Validators.required],
        }),
      },
      { validators: passwordsMatch },
    );
  }

  get hasPasswordMismatch(): boolean {
    return this.hasError(PASSWORD_MISMATCH) && this.controls.confirmPassword.touched;
  }

  toRequest(): IRegisterRequest {
    const { email, password } = this.getRawValue();
    return { email: email.trim(), password };
  }

  getFormControlNames() {
    return getFormControlsNames(this);
  }
}

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const { password, confirmPassword } = group.value as TRegisterForm;
  return confirmPassword && password !== confirmPassword ? { [PASSWORD_MISMATCH]: true } : null;
}
