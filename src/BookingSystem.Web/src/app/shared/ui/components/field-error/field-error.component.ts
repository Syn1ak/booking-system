import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, ValidationErrors } from '@angular/forms';
import { map, startWith, switchMap } from 'rxjs';

export type TErrorMessages = Record<string, (error: unknown) => string>;

const DEFAULT_MESSAGES: TErrorMessages = {
  required: () => 'This field is required.',
  email: () => 'Enter a valid email address.',
  minlength: (error) =>
    `Use at least ${(error as { requiredLength: number }).requiredLength} characters.`,
  maxlength: (error) =>
    `Use at most ${(error as { requiredLength: number }).requiredLength} characters.`,
  server: (error) => String(error),
};

/**
 * Shows the first error of a touched control. Pair with `[class.is-invalid]` on the input and
 * put this directly after it, so Bootstrap's `invalid-feedback` sibling rule reveals it.
 */
@Component({
  selector: 'app-field-error',
  template: `
    @if ($message(); as message) {
      <div class="invalid-feedback d-block">{{ message }}</div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FieldErrorComponent {
  $control = input.required<AbstractControl>({ alias: 'control' });
  $messages = input<TErrorMessages>({}, { alias: 'messages' });

  private readonly $errors = toSignal(
    toObservable(this.$control).pipe(
      switchMap((control) =>
        control.events.pipe(
          startWith(null),
          map((): ValidationErrors | null => (control.touched ? control.errors : null)),
        ),
      ),
    ),
    { initialValue: null },
  );

  readonly $message = computed(() => {
    const errors = this.$errors();
    const messages = { ...DEFAULT_MESSAGES, ...this.$messages() };

    if (!errors) {
      return null;
    }

    const key = Object.keys(errors).find((candidate) => messages[candidate]);
    return key ? messages[key](errors[key]) : null;
  });
}
