import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Role } from '../../entities/auth/role.enum';
import { SESSION_EXPIRED_REASON, SessionService } from './session.service';

describe('SessionService', () => {
  let session: SessionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    session = TestBed.inject(SessionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => vi.useRealTimers());

  const signIn = async (roles: Role[], expiresInMs = 60_000) => {
    const login = firstValueFrom(session.login$({ email: 'a@example.com', password: 'secret' }));

    http.expectOne('/api/auth/login').flush({
      token: 'token-1',
      expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
      roles,
    });
    const me = http.expectOne('/api/auth/me');
    expect(me.request.headers.get('Authorization')).toBe('Bearer token-1');
    me.flush({ userId: 'u1', email: 'a@example.com', roles });

    return login;
  };

  it('signs in with the user from /me and derives capabilities from the role', async () => {
    await signIn([Role.User]);

    expect(session.$isAuthenticated()).toBe(true);
    expect(session.$user()?.email).toBe('a@example.com');
    expect(session.can('canManageRooms')).toBe(false);
  });

  it('grants admin capabilities to an admin', async () => {
    await signIn([Role.Admin]);

    expect(session.can('canManageRooms')).toBe(true);
    expect(session.can('canViewAllBookings')).toBe(true);
  });

  it('never writes the token to web storage', async () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem');

    await signIn([Role.User]);

    expect(setItem).not.toHaveBeenCalled();
  });

  it('expires at expiresAt and sends the user to login with the page they were on', async () => {
    vi.useFakeTimers();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await signIn([Role.User], 1_000);
    let signedOut = false;
    session.signedOut$.subscribe(() => (signedOut = true));

    vi.advanceTimersByTime(1_000);

    expect(session.$isAuthenticated()).toBe(false);
    expect(signedOut).toBe(true);
    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/', reason: SESSION_EXPIRED_REASON },
    });
  });
});
