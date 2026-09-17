import { inject, Injectable, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { IRoom } from '../../../../../../core/entities/rooms/room.dto';
import { injectHandleErrors } from '../../../../../../core/errors/handle-errors.util';
import { RoomsClient } from '../../../../../../core/services/api/rooms/rooms.client';

@Injectable()
export class RoomListFacade {
  private readonly roomsClient = inject(RoomsClient);
  private readonly handleErrors = injectHandleErrors();

  private readonly $roomsState = signal<IRoom[]>([]);
  private readonly $loadingState = signal(true);

  readonly $rooms = this.$roomsState.asReadonly();
  readonly $loading = this.$loadingState.asReadonly();

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
}
