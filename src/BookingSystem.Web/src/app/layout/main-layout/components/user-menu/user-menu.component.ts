import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { NgbDropdown, NgbDropdownMenu, NgbDropdownToggle } from '@ng-bootstrap/ng-bootstrap';
import { ICurrentUser } from '../../../../core/entities/auth/current-user.dto';

@Component({
  selector: 'app-user-menu',
  imports: [NgbDropdown, NgbDropdownToggle, NgbDropdownMenu],
  template: `
    <div ngbDropdown placement="bottom-end" display="dynamic">
      <button
        type="button"
        class="btn btn-link p-0 d-flex align-items-center gap-2 text-decoration-none text-body"
        ngbDropdownToggle
        aria-label="Account menu"
      >
        <span class="app-avatar" aria-hidden="true">{{ $initial() }}</span>
      </button>
      <div ngbDropdownMenu class="app-user-menu shadow border-0 p-2">
        <div class="px-2 py-2">
          <div class="fw-semibold text-truncate">{{ $user().email }}</div>
          <div class="d-flex flex-wrap gap-1 mt-2">
            @for (role of $user().roles; track role) {
              <span class="badge bg-primary-subtle text-primary-emphasis">{{ role }}</span>
            }
          </div>
        </div>
        <hr class="dropdown-divider" />
        <button
          type="button"
          class="dropdown-item rounded-2 d-flex gap-2"
          (click)="$signOut.emit()"
        >
          <i class="bi bi-box-arrow-right" aria-hidden="true"></i> Sign out
        </button>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserMenuComponent {
  $user = input.required<ICurrentUser>({ alias: 'user' });
  $signOut = output<void>({ alias: 'signOut' });

  readonly $initial = computed(() => (this.$user().email ?? '?').charAt(0).toUpperCase());
}
