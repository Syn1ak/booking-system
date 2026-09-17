import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  provideRouter,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { SessionService } from '../services/session/session.service';
import { anonymousGuard } from './anonymous.guard';
import { authenticatedGuard } from './authenticated.guard';
import { capabilityGuard } from './capability.guard';

describe('guards', () => {
  const session = { $isAuthenticated: vi.fn(), can: vi.fn() };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: SessionService, useValue: session }],
    });
  });

  const run = (guard: CanActivateFn, url = '/rooms/r1?date=2026-09-17') =>
    TestBed.runInInjectionContext(() =>
      guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    );

  const urlOf = (result: unknown) => (result as UrlTree).toString();

  it('sends an anonymous visitor to login, remembering where they were going', () => {
    session.$isAuthenticated.mockReturnValue(false);

    expect(urlOf(run(authenticatedGuard))).toBe(
      '/login?returnUrl=%2Frooms%2Fr1%3Fdate%3D2026-09-17',
    );
  });

  it('keeps a signed-in user off the login page', () => {
    session.$isAuthenticated.mockReturnValue(true);

    expect(urlOf(run(anonymousGuard))).toBe('/rooms');
  });

  it('allows a page only with its capability', () => {
    session.can.mockReturnValue(false);
    expect(urlOf(run(capabilityGuard('canManageRooms')))).toBe('/rooms');

    session.can.mockReturnValue(true);
    expect(run(capabilityGuard('canManageRooms'))).toBe(true);
  });
});
