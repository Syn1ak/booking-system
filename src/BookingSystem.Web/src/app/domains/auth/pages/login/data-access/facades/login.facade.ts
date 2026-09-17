import { HttpStatusCode } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ILoginRequest } from '../../../../../../core/entities/auth/login.dto';
import { injectHandleErrors } from '../../../../../../core/errors/handle-errors';
import { SessionService } from '../../../../../../core/services/session/session.service';
import { safeReturnUrl } from '../../../../../../core/utils/navigation/safe-return-url.util';

@Injectable()
export class LoginFacade {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly handleErrors = injectHandleErrors();

  private readonly $pendingState = signal(false);
  private readonly $errorState = signal<string | null>(null);

  readonly $pending = this.$pendingState.asReadonly();
  readonly $error = this.$errorState.asReadonly();

  login(request: ILoginRequest, returnUrl: string | null): void {
    this.$pendingState.set(true);
    this.$errorState.set(null);

    this.session
      .login$(request)
      .pipe(
        // One message for both cases: the API deliberately does not say which was wrong.
        this.handleErrors({
          [HttpStatusCode.Unauthorized]: () =>
            this.$errorState.set('That email and password do not match an account.'),
        }),
        finalize(() => this.$pendingState.set(false)),
      )
      .subscribe(() => void this.router.navigateByUrl(safeReturnUrl(returnUrl)));
  }

  clearError(): void {
    this.$errorState.set(null);
  }
}
