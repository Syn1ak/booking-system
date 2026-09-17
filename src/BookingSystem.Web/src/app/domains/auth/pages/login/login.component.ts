import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SESSION_EXPIRED_REASON } from '../../../../core/services/session/session.service';
import { FieldErrorComponent } from '../../../../shared/ui/components/field-error/field-error.component';
import { LoginFacade } from './data-access/facades/login.facade';
import { LoginFormHandler, TLoginForm } from './models/login.form';

// Shown in every environment, deployed included: the deployed site is a reviewer's demo, and
// these credentials are the seeded ones from the repository, so hiding the buttons would hide
// nothing while costing a reviewer the fastest way in.
const DEMO_ACCOUNTS: readonly (TLoginForm & { label: string })[] = [
  { label: 'Demo admin', email: 'admin@example.com', password: 'Admin123!' },
  { label: 'Demo user', email: 'user@example.com', password: 'User123!' },
];

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, FieldErrorComponent],
  templateUrl: './login.component.html',
  providers: [LoginFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class LoginComponent {
  readonly expiredReason = SESSION_EXPIRED_REASON;
  readonly demoAccounts = DEMO_ACCOUNTS;

  readonly facade = inject(LoginFacade);
  private readonly destroyRef = inject(DestroyRef);

  $returnUrl = input<string | null>(null, { alias: 'returnUrl' });
  $reason = input<string | null>(null, { alias: 'reason' });

  readonly $showPassword = signal(false);

  readonly form = new LoginFormHandler();
  readonly controlNames = this.form.getFormControlNames();

  constructor() {
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.facade.clearError());
  }

  submit(): void {
    if (this.form.invalid) {
      return this.form.markAllAsTouched();
    }

    this.facade.login(this.form.toRequest(), this.$returnUrl());
  }

  useDemoAccount({ email, password }: TLoginForm): void {
    this.form.fill({ email, password });
  }
}
