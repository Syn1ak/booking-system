import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgbCollapse, NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { ColorModeService } from '../../../../core/services/color-mode/color-mode.service';
import { SessionService } from '../../../../core/services/session/session.service';
import { NAV_LINKS } from '../../constants/nav-links.constant';
import { UserMenuComponent } from '../user-menu/user-menu.component';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive, NgbCollapse, NgbTooltip, UserMenuComponent],
  templateUrl: './header.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeaderComponent {
  readonly session = inject(SessionService);
  readonly colorMode = inject(ColorModeService);

  readonly $menuOpen = signal(false);
  readonly $links = computed(() => {
    const capabilities = this.session.$capabilities();
    return NAV_LINKS.filter((link) => !link.capability || capabilities[link.capability]);
  });

  closeMenu(): void {
    this.$menuOpen.set(false);
  }
}
