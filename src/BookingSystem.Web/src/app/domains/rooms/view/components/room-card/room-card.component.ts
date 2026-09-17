import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IRoom } from '../../../../../core/entities/rooms/room.dto';
import { TimeOnlyPipe } from '../../../../../shared/ui/pipes/time-only.pipe';
import { slotsPerDay } from '../../../utils/room-hours.util';

@Component({
  selector: 'app-room-card',
  imports: [RouterLink, TimeOnlyPipe],
  template: `
    <article class="card h-100" [class.card-interactive]="$link()">
      <div class="card-body d-flex flex-column gap-3">
        <div class="d-flex align-items-start gap-3">
          <span class="icon-tile" aria-hidden="true"><i class="bi bi-door-open"></i></span>
          <div class="min-w-0 flex-grow-1">
            <h2 class="h5 mb-1 text-truncate">
              @if ($link(); as link) {
                <a class="stretched-link text-reset text-decoration-none" [routerLink]="link">{{
                  $room().name
                }}</a>
              } @else {
                {{ $room().name }}
              }
            </h2>
            <div class="text-body-secondary small">
              <i class="bi bi-clock me-1" aria-hidden="true"></i>
              {{ $room().opensAtUtc | timeOnly }}–{{ $room().closesAtUtc | timeOnly }} UTC
            </div>
          </div>
          @if ($link()) {
            <i class="bi bi-chevron-right text-body-tertiary" aria-hidden="true"></i>
          }
        </div>

        <div class="d-flex flex-wrap gap-2 mt-auto">
          <span class="badge bg-body-tertiary text-body-secondary border">
            <i class="bi bi-hourglass-split me-1" aria-hidden="true"></i
            >{{ $room().slotLengthMinutes }}-minute slots
          </span>
          <span class="badge bg-body-tertiary text-body-secondary border">
            <i class="bi bi-grid-3x3-gap me-1" aria-hidden="true"></i>{{ $slotsPerDay() }} per day
          </span>
        </div>
      </div>
      <ng-content select="[roomCardFooter]" />
    </article>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomCardComponent {
  $room = input.required<IRoom>({ alias: 'room' });
  $link = input<string | unknown[] | null>(null, { alias: 'link' });

  readonly $slotsPerDay = computed(() => slotsPerDay(this.$room()));
}
