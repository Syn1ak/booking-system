import { FormControl, Validators } from '@angular/forms';
import { ILoginRequest } from '../../../../../core/entities/auth/login.dto';
import { ControlsOf } from '../../../../../core/utils/form/controls-of.util';
import { FormHandler } from '../../../../../core/utils/form/form-handler.abstraction';
import { getFormControlsNames } from '../../../../../core/utils/form/get-form-controls-names.util';

export type TLoginForm = {
  email: string;
  password: string;
};

export class LoginFormHandler extends FormHandler<ControlsOf<TLoginForm>> {
  constructor() {
    super({
      email: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.email],
      }),
      password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    });
  }

  fill(credentials: TLoginForm): void {
    this.setValue(credentials);
  }

  toRequest(): ILoginRequest {
    const { email, password } = this.getRawValue();
    return { email: email.trim(), password };
  }

  getFormControlNames() {
    return getFormControlsNames(this);
  }
}
