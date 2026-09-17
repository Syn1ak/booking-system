import { HttpStatusCode } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { IRoom } from '../../../../../../core/entities/rooms/room.dto';
import { injectHandleErrors } from '../../../../../../core/errors/handle-errors.util';
import { isValidationProblem } from '../../../../../../core/errors/problem-details.util';
import { RoomsClient } from '../../../../../../core/services/api/rooms/rooms.client';
import { ToastService } from '../../../../../../core/services/notifications/toast.service';
import { applyValidationErrors } from '../../../../../../core/utils/form/apply-validation-errors.util';
import { RoomFormHandler } from '../../models/room.form';

@Injectable()
export class ManageRoomsFacade {
  private readonly roomsClient = inject(RoomsClient);
  private readonly toasts = inject(ToastService);
  private readonly handleErrors = injectHandleErrors();

  private readonly $roomsState = signal<IRoom[]>([]);
  private readonly $loadingState = signal(true);
  private readonly $savingState = signal(false);
  private readonly $saveErrorState = signal<string | null>(null);
  private readonly $deactivatingId = signal<string | null>(null);

  readonly $rooms = this.$roomsState.asReadonly();
  readonly $loading = this.$loadingState.asReadonly();
  readonly $saving = this.$savingState.asReadonly();
  readonly $saveError = this.$saveErrorState.asReadonly();
  readonly $deactivating = this.$deactivatingId.asReadonly();

  load(): void {
    this.$loadingState.set(true);

    this.roomsClient
      .getRooms$()
      .pipe(
        this.handleErrors(),
        finalize(() => this.$loadingState.set(false)),
      )
      .subscribe((rooms) => this.$roomsState.set(rooms));
  }

  save(form: RoomFormHandler, room: IRoom | null, onSaved: () => void): void {
    const request = form.toRequest();
    this.$savingState.set(true);
    this.$saveErrorState.set(null);

    const save$ = room
      ? this.roomsClient.updateRoom$(room.roomId, request)
      : this.roomsClient.createRoom$(request);

    save$
      .pipe(
        this.handleErrors({
          [HttpStatusCode.BadRequest]: (problem) => {
            const unmatched = isValidationProblem(problem)
              ? applyValidationErrors(form)(problem)
              : [];
            this.$saveErrorState.set(unmatched[0] ?? problem.detail ?? null);
          },
          // A slot length change while bookings stand; the detail names how many.
          [HttpStatusCode.Conflict]: (problem) =>
            this.$saveErrorState.set(
              problem.detail ?? problem.title ?? 'The room could not be saved.',
            ),
          [HttpStatusCode.NotFound]: () => {
            this.toasts.warning('This room no longer exists');
            onSaved();
            this.load();
          },
        }),
        finalize(() => this.$savingState.set(false)),
      )
      .subscribe((saved) => {
        this.toasts.success(room ? 'Room updated' : 'Room created', saved.name);
        onSaved();
        this.load();
      });
  }

  deactivate(room: IRoom): void {
    this.$deactivatingId.set(room.roomId);

    this.roomsClient
      .deactivateRoom$(room.roomId)
      .pipe(
        this.handleErrors(),
        finalize(() => {
          this.$deactivatingId.set(null);
          this.load();
        }),
      )
      .subscribe(() =>
        this.toasts.success('Room deactivated', `${room.name} is no longer listed.`),
      );
  }

  clearSaveError(): void {
    this.$saveErrorState.set(null);
  }
}
