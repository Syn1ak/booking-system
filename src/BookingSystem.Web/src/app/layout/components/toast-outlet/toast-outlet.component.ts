import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgbToast } from '@ng-bootstrap/ng-bootstrap';
import { TToastTone } from '../../../core/models/notifications/toast.types';
import { ToastService } from '../../../core/services/notifications/toast.service';

const TONE_ICONS: Record<TToastTone, string> = {
  success: 'bi-check-circle-fill text-success',
  info: 'bi-info-circle-fill text-info',
  warning: 'bi-exclamation-triangle-fill text-warning',
  danger: 'bi-x-octagon-fill text-danger',
};

@Component({
  selector: 'app-toast-outlet',
  imports: [NgbToast],
  template: `
    <div class="app-toasts" aria-live="polite" aria-atomic="false">
      @for (toast of toasts.$items(); track toast.id) {
        <ngb-toast
          class="border-0 shadow"
          [autohide]="true"
          [delay]="toast.tone === 'danger' ? 8000 : 5000"
          (hidden)="toasts.dismiss(toast.id)"
        >
          <div class="d-flex gap-3 align-items-start">
            <i class="bi fs-5 lh-1 mt-1" [class]="toneIcons[toast.tone]" aria-hidden="true"></i>
            <div class="flex-grow-1">
              <div class="fw-semibold">{{ toast.title }}</div>
              @if (toast.message) {
                <div class="text-body-secondary small mt-1">{{ toast.message }}</div>
              }
            </div>
            <button
              type="button"
              class="btn-close btn-sm"
              aria-label="Dismiss"
              (click)="toasts.dismiss(toast.id)"
            ></button>
          </div>
        </ngb-toast>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToastOutletComponent {
  readonly toneIcons = TONE_ICONS;

  readonly toasts = inject(ToastService);
}
