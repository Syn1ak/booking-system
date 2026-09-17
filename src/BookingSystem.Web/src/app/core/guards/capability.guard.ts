import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TCapability } from '../models/session/capability.types';
import { SessionService } from '../services/session/session.service';

/** Hiding a page is courtesy; the API enforces the same rule on every request. */
export function capabilityGuard(capability: TCapability): CanActivateFn {
  return () => inject(SessionService).can(capability) || inject(Router).createUrlTree(['/rooms']);
}
