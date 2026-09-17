import { TestBed } from '@angular/core/testing';
import { HubConnection, HubConnectionState } from '@microsoft/signalr';
import { firstValueFrom, Subject } from 'rxjs';
import { SessionService } from '../session/session.service';
import { SCHEDULE_HUB_CONNECTION_FACTORY } from './schedule-hub-connection.factory';
import { ScheduleHubClient } from './schedule-hub.client';

class FakeConnection {
  state = HubConnectionState.Disconnected;
  readonly handlers = new Map<string, (...args: unknown[]) => void>();
  reconnected: () => void = () => undefined;
  reconnecting: () => void = () => undefined;
  closed: () => void = () => undefined;

  readonly invoke = vi.fn(async (_method: string, _roomId: string) => undefined);
  readonly start = vi.fn(async () => {
    this.state = HubConnectionState.Connected;
  });
  readonly stop = vi.fn(async () => {
    this.state = HubConnectionState.Disconnected;
  });

  on(name: string, handler: (...args: unknown[]) => void) {
    this.handlers.set(name, handler);
  }
  onreconnected(callback: () => void) {
    this.reconnected = callback;
  }
  onreconnecting(callback: () => void) {
    this.reconnecting = callback;
  }
  onclose(callback: () => void) {
    this.closed = callback;
  }
}

describe('ScheduleHubClient', () => {
  let connection: FakeConnection;
  let signedOut: Subject<void>;
  let hub: ScheduleHubClient;

  beforeEach(() => {
    connection = new FakeConnection();
    signedOut = new Subject<void>();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: SessionService,
          useValue: { signedOut$: signedOut, $token: () => 'token', $isAuthenticated: () => true },
        },
        {
          provide: SCHEDULE_HUB_CONNECTION_FACTORY,
          useValue: async () => connection as unknown as HubConnection,
        },
      ],
    });
    hub = TestBed.inject(ScheduleHubClient);
  });

  it('connects on the first watch and joins the room', async () => {
    await firstValueFrom(hub.watch$('r1'), { defaultValue: undefined });

    expect(connection.start).toHaveBeenCalledTimes(1);
    expect(connection.invoke).toHaveBeenCalledWith('Watch', 'r1');
    expect(hub.$state()).toBe('connected');
  });

  it('re-joins every watched room after a reconnect, then tells watchers to refetch', async () => {
    await firstValueFrom(hub.watch$('r1'), { defaultValue: undefined });
    await firstValueFrom(hub.watch$('r2'), { defaultValue: undefined });
    connection.invoke.mockClear();
    let reconnected = false;
    hub.reconnected$.subscribe(() => (reconnected = true));

    connection.reconnecting();
    expect(hub.$state()).toBe('reconnecting');
    connection.reconnected();
    await vi.waitFor(() => expect(reconnected).toBe(true));

    expect(connection.invoke).toHaveBeenCalledWith('Watch', 'r1');
    expect(connection.invoke).toHaveBeenCalledWith('Watch', 'r2');
  });

  it('leaves a room only when its last watcher leaves', async () => {
    await firstValueFrom(hub.watch$('r1'), { defaultValue: undefined });
    await firstValueFrom(hub.watch$('r1'), { defaultValue: undefined });

    await firstValueFrom(hub.unwatch$('r1'), { defaultValue: undefined });
    expect(connection.invoke).not.toHaveBeenCalledWith('Unwatch', 'r1');

    await firstValueFrom(hub.unwatch$('r1'), { defaultValue: undefined });
    expect(connection.invoke).toHaveBeenCalledWith('Unwatch', 'r1');
  });

  it('forwards pushed events', async () => {
    await firstValueFrom(hub.watch$('r1'), { defaultValue: undefined });
    const received: string[] = [];
    hub.scheduleReset$.subscribe((roomId) => received.push(roomId));

    connection.handlers.get('ScheduleReset')?.('r1');

    expect(received).toEqual(['r1']);
  });

  it('does not throw at the watcher when the hub is unreachable', async () => {
    connection.start.mockRejectedValueOnce(new Error('negotiate failed'));

    await expect(
      firstValueFrom(hub.watch$('r1'), { defaultValue: undefined }),
    ).resolves.toBeUndefined();
    expect(hub.$state()).toBe('disconnected');
  });

  it('keeps retrying after automatic reconnection gives up, then re-joins and asks for a refetch', async () => {
    await firstValueFrom(hub.watch$('r1'), { defaultValue: undefined });
    vi.useFakeTimers();
    let reconnected = false;
    hub.reconnected$.subscribe(() => (reconnected = true));
    connection.invoke.mockClear();
    connection.start.mockRejectedValueOnce(new Error('ERR_CONNECTION_REFUSED'));

    connection.state = HubConnectionState.Disconnected;
    connection.closed();
    expect(hub.$state()).toBe('disconnected');

    await vi.advanceTimersByTimeAsync(5_000);
    expect(hub.$state()).toBe('disconnected');

    await vi.advanceTimersByTimeAsync(5_000);
    vi.useRealTimers();

    expect(hub.$state()).toBe('connected');
    expect(connection.invoke).toHaveBeenCalledWith('Watch', 'r1');
    expect(reconnected).toBe(true);
  });

  it('stops and forgets its rooms on sign-out', async () => {
    await firstValueFrom(hub.watch$('r1'), { defaultValue: undefined });

    signedOut.next();
    await vi.waitFor(() => expect(connection.stop).toHaveBeenCalled());

    expect(hub.$isWatching()).toBe(false);
  });
});
