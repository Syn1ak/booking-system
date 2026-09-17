import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../services/session/session.service';

export const anonymousGuard: CanActivateFn = () =>
  !inject(SessionService).$isAuthenticated() || inject(Router).createUrlTree(['/rooms']);
