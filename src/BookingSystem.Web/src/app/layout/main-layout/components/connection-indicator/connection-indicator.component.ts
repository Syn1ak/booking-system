import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { THubState } from '../../../../core/models/realtime/hub-state.types';
import { ScheduleHubClient } from '../../../../core/services/realtime/schedule-hub.client';

type TIndicator = { label: string; tooltip: string; className: string };

const INDICATORS: Record<THubState, TIndicator> = {
  connected: {
    label: 'Live',
    tooltip: 'Bookings by others appear here as they happen.',
    className: 'bg-success-subtle text-success-emphasis',
  },
  connecting: {
    label: 'Connecting…',
    tooltip: 'Connecting to live updates.',
    className: 'bg-warning-subtle text-warning-emphasis',
  },
  reconnecting: {
    label: 'Reconnecting…',
    tooltip: 'Live updates paused. The schedule refreshes once reconnected.',
    className: 'bg-warning-subtle text-warning-emphasis',
  },
  disconnected: {
    label: 'Offline',
    tooltip: 'Live updates unavailable. Bookings still work; reopen the schedule to refresh it.',
    className: 'bg-danger-subtle text-danger-emphasis',
  },
};

@Component({
  selector: 'app-connection-indicator',
  imports: [NgbTooltip],
  template: `
    @if (hub.$isWatching()) {
      <span
        class="badge d-inline-flex align-items-center gap-2 py-2 px-3"
        [class]="$indicator().className"
        [ngbTooltip]="$indicator().tooltip"
        tabindex="0"
        role="status"
      >
        <span
          [class]="hub.$state() === 'connected' ? 'live-dot' : 'status-dot'"
          aria-hidden="true"
        ></span>
        {{ $indicator().label }}
      </span>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConnectionIndicatorComponent {
  readonly hub = inject(ScheduleHubClient);

  readonly $indicator = computed(() => INDICATORS[this.hub.$state()]);
}
