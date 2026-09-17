import { ChangeDetectionStrategy, Component, inject, Injector, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { IRoom } from '../../../../core/entities/rooms/room.dto';
import { openConfirmDialog } from '../../../../shared/ui/components/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../../../shared/ui/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../shared/ui/components/page-header/page-header.component';
import { SkeletonComponent } from '../../../../shared/ui/components/skeleton/skeleton.component';
import { RoomCardComponent } from '../../view/components/room-card/room-card.component';
import { ManageRoomsFacade } from './data-access/facades/manage-rooms.facade';
import { openRoomFormModal } from './view/components/room-form-modal/room-form-modal.component';

@Component({
  selector: 'app-manage-rooms',
  imports: [PageHeaderComponent, EmptyStateComponent, SkeletonComponent, RoomCardComponent],
  templateUrl: './manage-rooms.component.html',
  providers: [ManageRoomsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class ManageRoomsComponent implements OnInit {
  readonly placeholders = [1, 2, 3];

  readonly facade = inject(ManageRoomsFacade);
  private readonly modals = inject(NgbModal);
  private readonly injector = inject(Injector);

  ngOnInit(): void {
    this.facade.load();
  }

  openForm(room: IRoom | null = null): void {
    openRoomFormModal(this.modals, this.injector, room);
  }

  async confirmDeactivate(room: IRoom): Promise<void> {
    const confirmed = await openConfirmDialog(this.modals, this.injector, {
      title: `Deactivate ${room.name}?`,
      message:
        'It disappears from room listings and can no longer be booked. Existing bookings are kept and stay visible in booking history.',
      confirmLabel: 'Deactivate room',
      tone: 'danger',
    });

    if (confirmed) {
      this.facade.deactivate(room);
    }
  }
}
