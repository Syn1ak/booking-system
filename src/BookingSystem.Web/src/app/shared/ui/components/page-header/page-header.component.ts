import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  template: `
    <header class="d-flex flex-wrap align-items-center gap-3 mb-4">
      @if ($icon(); as icon) {
        <span class="icon-tile" aria-hidden="true"><i class="bi" [class]="'bi-' + icon"></i></span>
      }
      <div class="flex-grow-1 min-w-0">
        <ng-content select="[pageHeaderEyebrow]" />
        <h1 class="h3 mb-0 text-truncate">{{ $title() }}</h1>
        @if ($subtitle(); as subtitle) {
          <p class="text-body-secondary mb-0 mt-1">{{ subtitle }}</p>
        }
      </div>
      <div class="d-flex flex-wrap align-items-center gap-2">
        <ng-content select="[pageHeaderActions]" />
      </div>
    </header>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageHeaderComponent {
  $title = input.required<string>({ alias: 'title' });
  $subtitle = input<string | null>(null, { alias: 'subtitle' });
  $icon = input<string | null>(null, { alias: 'icon' });
}
