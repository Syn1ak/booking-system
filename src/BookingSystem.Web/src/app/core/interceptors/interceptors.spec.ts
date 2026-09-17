import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SessionService } from '../services/session/session.service';
import { authTokenInterceptor } from './auth-token.interceptor';
import { unauthorizedInterceptor } from './unauthorized.interceptor';

describe('interceptors', () => {
  const $token = signal<string | null>('token-1');
  const $isAuthenticated = signal(true);
  const session = { $token, $isAuthenticated, expire: vi.fn() };
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    $token.set('token-1');
    $isAuthenticated.set(true);
    session.expire.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionService, useValue: session },
        provideHttpClient(withInterceptors([authTokenInterceptor, unauthorizedInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  it('attaches the bearer token to API requests only', () => {
    http.get('/api/rooms').subscribe();
    http.get('/media/logo.svg').subscribe();

    expect(controller.expectOne('/api/rooms').request.headers.get('Authorization')).toBe(
      'Bearer token-1',
    );
    expect(controller.expectOne('/media/logo.svg').request.headers.has('Authorization')).toBe(
      false,
    );
  });

  it('expires the session on a 401 from a signed-in request', () => {
    http.get('/api/rooms').subscribe({ error: () => undefined });

    controller.expectOne('/api/rooms').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(session.expire).toHaveBeenCalled();
  });

  it('leaves a failed login alone, so its error reaches the form', () => {
    $token.set(null);
    $isAuthenticated.set(false);
    http.post('/api/auth/login', {}).subscribe({ error: () => undefined });

    controller
      .expectOne('/api/auth/login')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(session.expire).not.toHaveBeenCalled();
  });
});
