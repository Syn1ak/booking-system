import { HttpErrorResponse, HttpInterceptorFn, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { SessionService } from '../services/session/session.service';

/** A 401 from a signed-in request means the token is no longer accepted: sign out. */
export const unauthorizedInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === HttpStatusCode.Unauthorized &&
        session.$isAuthenticated()
      ) {
        session.expire();
      }

      return throwError(() => error);
    }),
  );
};
