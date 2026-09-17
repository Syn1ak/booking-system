import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastOutletComponent } from '../components/toast-outlet/toast-outlet.component';
import { HeaderComponent } from './components/header/header.component';

@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, HeaderComponent, ToastOutletComponent],
  template: `
    <div class="app-shell">
      <a class="visually-hidden-focusable position-absolute m-2 btn btn-primary" href="#content">
        Skip to content
      </a>
      <app-header />
      <main id="content" class="app-main" tabindex="-1">
        <div class="container">
          <router-outlet />
        </div>
      </main>
      <footer class="py-4 border-top">
        <div
          class="container small text-body-secondary d-flex flex-wrap justify-content-between gap-2"
        >
          <span>RoomBook — meeting rooms, booked once.</span>
          <span><i class="bi bi-globe2 me-1" aria-hidden="true"></i>All times are UTC</span>
        </div>
      </footer>
    </div>
    <app-toast-outlet />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MainLayoutComponent {}
