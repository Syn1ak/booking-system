import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  template: `
    <div class="card border-0">
      <div class="card-body text-center py-5 px-4">
        <span class="icon-tile mb-3" aria-hidden="true"
          ><i class="bi" [class]="'bi-' + $icon()"></i
        ></span>
        <h2 class="h5 mb-1">{{ $title() }}</h2>
        @if ($message(); as message) {
          <p class="text-body-secondary mb-0 mx-auto" style="max-width: 32rem">{{ message }}</p>
        }
        <div class="d-flex justify-content-center gap-2 mt-3 empty-state-actions">
          <ng-content />
        </div>
      </div>
    </div>
  `,
  styles: `
    .empty-state-actions:empty {
      display: none !important;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmptyStateComponent {
  $icon = input('inbox', { alias: 'icon' });
  $title = input.required<string>({ alias: 'title' });
  $message = input<string | null>(null, { alias: 'message' });
}
