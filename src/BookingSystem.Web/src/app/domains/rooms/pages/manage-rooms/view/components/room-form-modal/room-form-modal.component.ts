import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  InjectionToken,
  Injector,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { NgbActiveModal, NgbModal, NgbTimepicker } from '@ng-bootstrap/ng-bootstrap';
import { IRoom } from '../../../../../../../core/entities/rooms/room.dto';
import { FieldErrorComponent } from '../../../../../../../shared/ui/components/field-error/field-error.component';
import { getSlotLengthI18List } from '../../../../../constants/slot-length.constant';
import { ManageRoomsFacade } from '../../../data-access/facades/manage-rooms.facade';
import { NAME_MAX_LENGTH, RoomFormHandler } from '../../../models/room.form';

const ROOM_TO_EDIT = new InjectionToken<IRoom | null>('ROOM_TO_EDIT');

@Component({
  selector: 'app-room-form-modal',
  imports: [ReactiveFormsModule, NgbTimepicker, FieldErrorComponent],
  templateUrl: './room-form-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomFormModalComponent {
  readonly slotLengthOptions = getSlotLengthI18List();
  readonly nameMaxLength = NAME_MAX_LENGTH;

  readonly modal = inject(NgbActiveModal);
  readonly facade = inject(ManageRoomsFacade);
  readonly room = inject(ROOM_TO_EDIT);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = new RoomFormHandler();
  readonly controlNames = this.form.getFormControlNames();

  constructor() {
    this.form.setFromRoom(this.room);
    this.facade.clearSaveError();
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.facade.clearSaveError());
  }

  submit(): void {
    if (this.form.invalid) {
      return this.form.markAllAsTouched();
    }

    this.facade.save(this.form, this.room, () => this.modal.close());
  }
}

/** Opened with the page's injector, so the modal shares the page's facade. */
export function openRoomFormModal(modals: NgbModal, injector: Injector, room: IRoom | null): void {
  modals.open(RoomFormModalComponent, {
    centered: true,
    size: 'lg',
    ariaLabelledBy: 'room-form-title',
    injector: Injector.create({
      providers: [{ provide: ROOM_TO_EDIT, useValue: room }],
      parent: injector,
    }),
  });
}
