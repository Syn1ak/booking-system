import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { map, Observable, Subject, switchMap } from 'rxjs';
import { ICurrentUser } from '../../entities/auth/current-user.dto';
import { ILoginRequest } from '../../entities/auth/login.dto';
import { Role } from '../../entities/auth/role.enum';
import { CAPABILITY_ROLES, TCapability } from '../../models/session/capability.types';
import { TSession } from '../../models/session/session.types';
import { AuthClient } from '../api/auth/auth.client';

export const SESSION_EXPIRED_REASON = 'expired';

/**
 * The token lives in this signal and nowhere else — never web storage (see auth.md). A page
 * refresh therefore signs the user out, which is the accepted cost of keeping it out of reach
 * of anything that can read storage.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly authClient = inject(AuthClient);
  private readonly router = inject(Router);

  private readonly $session = signal<TSession | null>(null);
  private readonly signedOutSubject = new Subject<void>();

  readonly $token = computed(() => this.$session()?.token ?? null);
  readonly $user = computed(() => this.$session()?.user ?? null);
  readonly $isAuthenticated = computed(() => this.$session() !== null);
  readonly $capabilities = computed(() => {
    const roles: readonly string[] = this.$session()?.user.roles ?? [];
    const entries = Object.entries(CAPABILITY_ROLES) as [TCapability, readonly Role[]][];

    return Object.fromEntries(
      entries.map(([capability, allowed]) => [
        capability,
        allowed.some((role) => roles.includes(role)),
      ]),
    ) as Record<TCapability, boolean>;
  });

  readonly signedOut$ = this.signedOutSubject.asObservable();

  private expiryTimer: ReturnType<typeof setTimeout> | undefined;

  login$(request: ILoginRequest): Observable<ICurrentUser> {
    return this.authClient
      .login$(request)
      .pipe(
        switchMap(({ token, expiresAt }) =>
          this.authClient
            .getCurrentUser$(token)
            .pipe(map((user) => this.start({ token, expiresAt, user }))),
        ),
      );
  }

  can(capability: TCapability): boolean {
    return this.$capabilities()[capability];
  }

  logout(): void {
    this.end();
    void this.router.navigate(['/login']);
  }

  expire(): void {
    const returnUrl = this.router.url;
    this.end();
    void this.router.navigate(['/login'], {
      queryParams: { returnUrl, reason: SESSION_EXPIRED_REASON },
    });
  }

  private start(session: TSession): ICurrentUser {
    this.$session.set(session);
    clearTimeout(this.expiryTimer);
    this.expiryTimer = setTimeout(
      () => this.expire(),
      new Date(session.expiresAt).getTime() - Date.now(),
    );
    return session.user;
  }

  private end(): void {
    if (!this.$session()) {
      return;
    }

    clearTimeout(this.expiryTimer);
    this.$session.set(null);
    this.signedOutSubject.next();
  }
}
