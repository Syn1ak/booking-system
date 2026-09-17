import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastOutletComponent } from '../components/toast-outlet/toast-outlet.component';

@Component({
  selector: 'app-auth-layout',
  imports: [RouterOutlet, ToastOutletComponent],
  template: `
    <main class="app-auth">
      <div class="app-auth-card">
        <div class="text-center mb-4">
          <span class="app-brand justify-content-center fs-4">
            <span class="app-brand-mark" aria-hidden="true"
              ><i class="bi bi-calendar2-check"></i
            ></span>
            RoomBook
          </span>
        </div>
        <router-outlet />
      </div>
    </main>
    <app-toast-outlet />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthLayoutComponent {}
