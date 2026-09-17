import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, EMPTY, MonoTypeOperatorFunction, throwError } from 'rxjs';
import { ToastService } from '../services/notifications/toast.service';
import { IProblemDetails } from './problem-details.types';
import { toProblemDetails } from './problem-details.util';

export type TErrorHandlers = Partial<Record<number | '*', (problem: IProblemDetails) => void>>;

/**
 * Handlers are keyed by status code, not by title: this API's titles are human sentences.
 * Anything unhandled becomes a toast, except 401, which the unauthorized interceptor has
 * already turned into a sign-out. A handled or toasted error completes the stream.
 */
export function injectHandleErrors() {
  const toasts = inject(ToastService);

  return <T>(handlers: TErrorHandlers = {}): MonoTypeOperatorFunction<T> =>
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const problem = toProblemDetails(error);
      const handler = handlers[problem.status] ?? handlers['*'];

      if (handler) {
        handler(problem);
      } else if (problem.status === 0) {
        toasts.danger('Cannot reach the server', 'Check your connection and try again.');
      } else if (problem.status !== HttpStatusCode.Unauthorized) {
        toasts.danger(
          problem.title ?? 'Something went wrong',
          problem.detail ?? 'Please try again in a moment.',
        );
      }

      return EMPTY;
    });
}
