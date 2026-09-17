import { PASSWORD_MISMATCH, RegisterFormHandler } from './register.form';

describe('RegisterFormHandler', () => {
  it('flags passwords that do not match', () => {
    const form = new RegisterFormHandler();

    form.setValue({ email: 'a@example.com', password: 'longenough', confirmPassword: 'different' });

    expect(form.hasError(PASSWORD_MISMATCH)).toBe(true);
  });

  it('sends only what the API accepts, with the email trimmed', () => {
    const form = new RegisterFormHandler();

    form.setValue({
      email: ' a@example.com ',
      password: 'longenough',
      confirmPassword: 'longenough',
    });

    expect(form.toRequest()).toEqual({ email: 'a@example.com', password: 'longenough' });
  });
});
