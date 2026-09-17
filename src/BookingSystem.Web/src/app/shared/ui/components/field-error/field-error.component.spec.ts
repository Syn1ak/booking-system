import { TestBed } from '@angular/core/testing';
import { FormControl, Validators } from '@angular/forms';
import { FieldErrorComponent } from './field-error.component';

describe('FieldErrorComponent', () => {
  const render = (control: FormControl) => {
    const fixture = TestBed.createComponent(FieldErrorComponent);
    fixture.componentRef.setInput('control', control);
    fixture.detectChanges();
    return fixture;
  };

  it('stays quiet until the control is touched', () => {
    const control = new FormControl('', Validators.required);
    const fixture = render(control);

    expect(fixture.nativeElement.textContent.trim()).toBe('');

    control.markAsTouched();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent.trim()).toBe('This field is required.');
  });

  it('shows a server message verbatim', () => {
    const control = new FormControl('x');
    const fixture = render(control);

    control.setErrors({ server: 'Name is taken.' });
    control.markAsTouched();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent.trim()).toBe('Name is taken.');
  });
});
