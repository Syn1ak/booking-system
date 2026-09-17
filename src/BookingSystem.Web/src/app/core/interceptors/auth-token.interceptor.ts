import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { SessionService } from '../services/session/session.service';

export const authTokenInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(SessionService).$token();

  if (!token || !request.url.startsWith('/api/') || request.headers.has('Authorization')) {
    return next(request);
  }

  return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
