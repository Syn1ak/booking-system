import { DOCUMENT, inject, Injectable, signal } from '@angular/core';
import { TColorMode } from '../../models/color-mode/color-mode.types';

/** Shared with the pre-paint script in index.html, which applies the mode before Angular runs. */
const STORAGE_KEY = 'color-mode';

@Injectable({ providedIn: 'root' })
export class ColorModeService {
  private readonly document = inject(DOCUMENT);

  private readonly $mode = signal<TColorMode>(
    this.document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'dark' : 'light',
  );

  readonly $current = this.$mode.asReadonly();

  toggle(): void {
    const next: TColorMode = this.$mode() === 'dark' ? 'light' : 'dark';

    this.$mode.set(next);
    this.document.documentElement.setAttribute('data-bs-theme', next);

    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Storage can be unavailable (private mode, blocked site data); the mode still applies.
    }
  }
}
