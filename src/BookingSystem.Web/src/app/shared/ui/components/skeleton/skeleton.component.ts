import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-skeleton',
  template: `<span class="skeleton" [style.width]="$width()" [style.height]="$height()"></span>`,
  host: { 'aria-hidden': 'true' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkeletonComponent {
  $width = input('100%', { alias: 'width' });
  $height = input('1rem', { alias: 'height' });
}
