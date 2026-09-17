import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ICurrentUser } from '../../../entities/auth/current-user.dto';
import { ILoginRequest, ILoginResponse } from '../../../entities/auth/login.dto';
import { IRegisterRequest, IRegisterResponse } from '../../../entities/auth/register.dto';

@Injectable({ providedIn: 'root' })
export class AuthClient {
  static readonly loginUrl = '/api/auth/login';

  private readonly http = inject(HttpClient);

  login$(request: ILoginRequest): Observable<ILoginResponse> {
    return this.http.post<ILoginResponse>(AuthClient.loginUrl, request);
  }

  register$(request: IRegisterRequest): Observable<IRegisterResponse> {
    return this.http.post<IRegisterResponse>('/api/auth/register', request);
  }

  getCurrentUser$(token: string): Observable<ICurrentUser> {
    return this.http.get<ICurrentUser>('/api/auth/me', {
      headers: { Authorization: `Bearer ${token}` },
    });
  }
}
