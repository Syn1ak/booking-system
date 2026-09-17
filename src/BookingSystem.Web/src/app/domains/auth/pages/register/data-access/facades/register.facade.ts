import { HttpStatusCode } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { finalize, switchMap } from 'rxjs';
import { AuthClient } from '../../../../../../core/services/api/auth/auth.client';
import { injectHandleErrors } from '../../../../../../core/errors/handle-errors';
import { isValidationProblem } from '../../../../../../core/errors/problem-details';
import { SessionService } from '../../../../../../core/services/session/session.service';
import { applyValidationErrors } from '../../../../../../core/utils/form/apply-validation-errors.util';
import { RegisterFormHandler } from '../../models/register.form';

@Injectable()
export class RegisterFacade {
  private readonly authClient = inject(AuthClient);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly handleErrors = injectHandleErrors();

  private readonly $pendingState = signal(false);
  private readonly $errorsState = signal<string[]>([]);

  readonly $pending = this.$pendingState.asReadonly();
  readonly $errors = this.$errorsState.asReadonly();

  register(form: RegisterFormHandler): void {
    const request = form.toRequest();
    this.$pendingState.set(true);
    this.$errorsState.set([]);

    this.authClient
      .register$(request)
      .pipe(
        switchMap(() => this.session.login$(request)),
        this.handleErrors({
          [HttpStatusCode.BadRequest]: (problem) =>
            this.$errorsState.set(
              isValidationProblem(problem)
                ? applyValidationErrors(form)(problem)
                : [problem.detail ?? 'The account could not be created.'],
            ),
        }),
        finalize(() => this.$pendingState.set(false)),
      )
      .subscribe(() => void this.router.navigateByUrl('/rooms'));
  }

  clearErrors(): void {
    this.$errorsState.set([]);
  }
}
