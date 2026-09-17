import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { EnvironmentProviders, provideBrowserGlobalErrorListeners, Provider } from '@angular/core';
import {
  provideRouter,
  Routes,
  withComponentInputBinding,
  withInMemoryScrolling,
  withViewTransitions,
} from '@angular/router';
import { authTokenInterceptor } from './interceptors/auth-token.interceptor';
import { unauthorizedInterceptor } from './interceptors/unauthorized.interceptor';

export interface ICoreOptions {
  routes: Routes;
}

export function provideCore({ routes }: ICoreOptions): (Provider | EnvironmentProviders)[] {
  return [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withViewTransitions(),
      withInMemoryScrolling({ scrollPositionRestoration: 'top' }),
    ),
    provideHttpClient(
      withFetch(),
      withInterceptors([authTokenInterceptor, unauthorizedInterceptor]),
    ),
  ];
}
