import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../../../core/services/session/session.service';
import { EmptyStateComponent } from '../../../../shared/ui/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../shared/ui/components/page-header/page-header.component';
import { SkeletonComponent } from '../../../../shared/ui/components/skeleton/skeleton.component';
import { RoomCardComponent } from '../../view/components/room-card/room-card.component';
import { RoomListFacade } from './data-access/facades/room-list.facade';

@Component({
  selector: 'app-room-list',
  imports: [
    RouterLink,
    PageHeaderComponent,
    EmptyStateComponent,
    SkeletonComponent,
    RoomCardComponent,
  ],
  templateUrl: './room-list.component.html',
  providers: [RoomListFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class RoomListComponent implements OnInit {
  readonly placeholders = [1, 2, 3];

  readonly facade = inject(RoomListFacade);
  readonly session = inject(SessionService);

  ngOnInit(): void {
    this.facade.load();
  }
}
