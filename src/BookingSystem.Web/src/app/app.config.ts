import { ApplicationConfig } from '@angular/core';
import { routes } from './app.routes';
import { provideCore } from './core/core.provider';

export const appConfig: ApplicationConfig = {
  providers: [provideCore({ routes })],
};
