import { InjectionToken } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { SCHEDULE_HUB } from '../../entities/realtime/schedule-hub.contract';

export type TScheduleHubConnectionFactory = (accessToken: () => string) => Promise<HubConnection>;

/**
 * The SignalR client is imported on first use, so it stays out of the initial bundle for pages
 * that never watch a room.
 *
 * The transport is deliberately left to negotiation: with Azure SignalR the negotiate response
 * redirects the browser to the service, and pinning a transport breaks that in ways that look
 * like an authentication failure.
 */
export const SCHEDULE_HUB_CONNECTION_FACTORY = new InjectionToken<TScheduleHubConnectionFactory>(
  'SCHEDULE_HUB_CONNECTION_FACTORY',
  {
    providedIn: 'root',
    factory: () => async (accessToken) => {
      const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr');

      return new HubConnectionBuilder()
        .withUrl(SCHEDULE_HUB.path, { accessTokenFactory: accessToken })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();
    },
  },
);
