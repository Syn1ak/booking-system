import {
  ChangeDetectionStrategy,
  Component,
  inject,
  InjectionToken,
  Injector,
} from '@angular/core';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';

export type TConfirmDialogOptions = {
  title: string;
  message: string;
  confirmLabel: string;
  tone?: 'primary' | 'danger';
};

const CONFIRM_DIALOG_OPTIONS = new InjectionToken<TConfirmDialogOptions>('CONFIRM_DIALOG_OPTIONS');

@Component({
  selector: 'app-confirm-dialog',
  template: `
    <div class="modal-body p-4">
      <div class="d-flex gap-3">
        <span
          class="icon-tile"
          [class.bg-danger-subtle]="isDanger"
          [class.text-danger]="isDanger"
          aria-hidden="true"
        >
          <i class="bi" [class]="isDanger ? 'bi-exclamation-triangle' : 'bi-question-circle'"></i>
        </span>
        <div>
          <h2 class="h5 mb-1" id="confirm-dialog-title">{{ options.title }}</h2>
          <p class="text-body-secondary mb-0">{{ options.message }}</p>
        </div>
      </div>
    </div>
    <div class="modal-footer border-0 pt-0 px-4 pb-4">
      <button type="button" class="btn btn-light" (click)="modal.dismiss()">Keep it</button>
      <button
        type="button"
        class="btn"
        [class]="isDanger ? 'btn-danger' : 'btn-primary'"
        (click)="modal.close(true)"
        ngbAutofocus
      >
        {{ options.confirmLabel }}
      </button>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialogComponent {
  readonly modal = inject(NgbActiveModal);
  readonly options = inject(CONFIRM_DIALOG_OPTIONS);

  readonly isDanger = this.options.tone === 'danger';
}

/** Resolves true on confirm and false on any dismissal, so callers never handle a rejection. */
export function openConfirmDialog(
  modals: NgbModal,
  injector: Injector,
  options: TConfirmDialogOptions,
): Promise<boolean> {
  const reference = modals.open(ConfirmDialogComponent, {
    centered: true,
    ariaLabelledBy: 'confirm-dialog-title',
    injector: Injector.create({
      providers: [{ provide: CONFIRM_DIALOG_OPTIONS, useValue: options }],
      parent: injector,
    }),
  });

  return reference.result.then(
    () => true,
    () => false,
  );
}
