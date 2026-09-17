import { computed, inject, Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionState } from '@microsoft/signalr';
import { defer, Observable, Subject } from 'rxjs';
import { SCHEDULE_HUB } from '../../entities/realtime/schedule-hub.contract';
import { ISlotChange } from '../../entities/realtime/slot-change.dto';
import { THubState } from '../../models/realtime/hub-state.types';
import { SessionService } from '../session/session.service';
import { SCHEDULE_HUB_CONNECTION_FACTORY } from './schedule-hub-connection.factory';

const RESTART_DELAY_MS = 5_000;

/**
 * Owns the one connection to the schedule hub and which rooms it watches. It never throws at a
 * caller: push is an optimisation over the schedule endpoint, so a hub that cannot connect
 * degrades the page to manual refresh rather than breaking it.
 *
 * Group membership does not survive a reconnect. This client re-joins every watched room and
 * then emits `reconnected$`; refetching is the watcher's job, because events sent during the
 * gap are gone.
 */
@Injectable({ providedIn: 'root' })
export class ScheduleHubClient {
  private readonly session = inject(SessionService);
  private readonly createConnection = inject(SCHEDULE_HUB_CONNECTION_FACTORY);

  private readonly $stateSignal = signal<THubState>('disconnected');
  private readonly $watchedRooms = signal<ReadonlyMap<string, number>>(new Map());
  private readonly slotChangedSubject = new Subject<ISlotChange>();
  private readonly scheduleResetSubject = new Subject<string>();
  private readonly reconnectedSubject = new Subject<void>();

  private connection: HubConnection | null = null;
  private starting: Promise<void> | null = null;
  private restartTimer: ReturnType<typeof setTimeout> | undefined;
  private stopping = false;

  readonly $state = this.$stateSignal.asReadonly();
  readonly $isWatching = computed(() => this.$watchedRooms().size > 0);

  readonly slotChanged$ = this.slotChangedSubject.asObservable();
  readonly scheduleReset$ = this.scheduleResetSubject.asObservable();
  readonly reconnected$ = this.reconnectedSubject.asObservable();

  constructor() {
    this.session.signedOut$.subscribe(() => void this.stop());
  }

  watch$(roomId: string): Observable<void> {
    return defer(() => this.watch(roomId));
  }

  unwatch$(roomId: string): Observable<void> {
    return defer(() => this.unwatch(roomId));
  }

  private async watch(roomId: string): Promise<void> {
    const watchers = this.$watchedRooms().get(roomId) ?? 0;
    this.setWatchers(roomId, watchers + 1);

    if (watchers > 0) {
      return;
    }

    const connection = await this.connect();

    if (connection.state === HubConnectionState.Connected) {
      await this.invoke(connection, SCHEDULE_HUB.methods.watch, roomId);
    }
  }

  private async unwatch(roomId: string): Promise<void> {
    const watchers = this.$watchedRooms().get(roomId) ?? 0;

    if (watchers === 0) {
      return;
    }

    this.setWatchers(roomId, watchers - 1);

    if (watchers === 1 && this.connection?.state === HubConnectionState.Connected) {
      await this.invoke(this.connection, SCHEDULE_HUB.methods.unwatch, roomId);
    }
  }

  private async connect(): Promise<HubConnection> {
    this.connection ??= this.build();

    if (this.connection.state === HubConnectionState.Disconnected) {
      this.starting ??= this.start(this.connection).finally(() => (this.starting = null));
    }

    await this.starting;
    return this.connection;
  }

  private build(): HubConnection {
    const connection = this.createConnection(() => this.session.$token() ?? '');

    connection.on(SCHEDULE_HUB.events.slotChanged, (change: ISlotChange) =>
      this.slotChangedSubject.next(change),
    );
    connection.on(SCHEDULE_HUB.events.scheduleReset, (roomId: string) =>
      this.scheduleResetSubject.next(roomId),
    );
    connection.onreconnecting(() => this.$stateSignal.set('reconnecting'));
    connection.onreconnected(() => void this.rejoin(connection));
    connection.onclose(() => {
      if (this.stopping) {
        return;
      }

      this.$stateSignal.set('disconnected');
      this.scheduleRestart();
    });

    return connection;
  }

  private async start(connection: HubConnection): Promise<void> {
    this.$stateSignal.set('connecting');

    try {
      await connection.start();
      this.$stateSignal.set('connected');
    } catch {
      this.$stateSignal.set('disconnected');
      this.scheduleRestart();
    }
  }

  private scheduleRestart(): void {
    if (this.restartTimer || !this.$isWatching() || !this.session.$isAuthenticated()) {
      return;
    }

    this.restartTimer = setTimeout(async () => {
      this.restartTimer = undefined;
      const connection = await this.connect();

      if (connection.state === HubConnectionState.Connected) {
        await this.rejoin(connection);
      }
    }, RESTART_DELAY_MS);
  }

  private async rejoin(connection: HubConnection): Promise<void> {
    this.$stateSignal.set('connected');

    await Promise.all(
      [...this.$watchedRooms().keys()].map((roomId) =>
        this.invoke(connection, SCHEDULE_HUB.methods.watch, roomId),
      ),
    );

    this.reconnectedSubject.next();
  }

  private async invoke(connection: HubConnection, method: string, roomId: string): Promise<void> {
    try {
      await connection.invoke(method, roomId);
    } catch (error) {
      console.warn(`Schedule hub ${method} failed for room ${roomId}`, error);
    }
  }

  private async stop(): Promise<void> {
    clearTimeout(this.restartTimer);
    this.restartTimer = undefined;
    this.$watchedRooms.set(new Map());

    const connection = this.connection;
    this.connection = null;

    if (connection) {
      this.stopping = true;
      await connection.stop();
      this.stopping = false;
    }

    this.$stateSignal.set('disconnected');
  }

  private setWatchers(roomId: string, count: number): void {
    this.$watchedRooms.update((rooms) => {
      const next = new Map(rooms);

      if (count > 0) {
        next.set(roomId, count);
      } else {
        next.delete(roomId);
      }

      return next;
    });
  }
}
