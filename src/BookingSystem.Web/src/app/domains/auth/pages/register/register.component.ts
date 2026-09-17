import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FieldErrorComponent } from '../../../../shared/ui/components/field-error/field-error.component';
import { RegisterFacade } from './data-access/facades/register.facade';
import { PASSWORD_MIN_LENGTH, RegisterFormHandler } from './models/register.form';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, FieldErrorComponent],
  templateUrl: './register.component.html',
  providers: [RegisterFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class RegisterComponent {
  readonly passwordMinLength = PASSWORD_MIN_LENGTH;

  readonly facade = inject(RegisterFacade);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = new RegisterFormHandler();
  readonly controlNames = this.form.getFormControlNames();

  constructor() {
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.facade.clearErrors());
  }

  submit(): void {
    if (this.form.invalid) {
      return this.form.markAllAsTouched();
    }

    this.facade.register(this.form);
  }
}
