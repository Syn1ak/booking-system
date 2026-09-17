import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, throwError, toArray } from 'rxjs';
import { ToastService } from '../services/notifications/toast.service';
import { injectHandleErrors } from './handle-errors';

describe('injectHandleErrors', () => {
  const fail = (status: number, body: unknown = null) =>
    throwError(() => new HttpErrorResponse({ status, error: body }));

  const setUp = () => {
    const handleErrors = TestBed.runInInjectionContext(() => injectHandleErrors());
    return { handleErrors, toasts: TestBed.inject(ToastService) };
  };

  it('calls the handler for the status and completes', async () => {
    const { handleErrors, toasts } = setUp();
    const conflict = vi.fn();

    const values = await firstValueFrom(
      fail(409, { title: 'Slot already booked' }).pipe(handleErrors({ 409: conflict }), toArray()),
    );

    expect(values).toEqual([]);
    expect(conflict).toHaveBeenCalledWith(
      expect.objectContaining({ status: 409, title: 'Slot already booked' }),
    );
    expect(toasts.$items()).toEqual([]);
  });

  it('toasts an unhandled server error', async () => {
    const { handleErrors, toasts } = setUp();

    await firstValueFrom(fail(500).pipe(handleErrors(), toArray()));

    expect(toasts.$items()).toHaveLength(1);
    expect(toasts.$items()[0].tone).toBe('danger');
  });

  it('stays silent on 401, which the interceptor has already handled', async () => {
    const { handleErrors, toasts } = setUp();

    await firstValueFrom(fail(401).pipe(handleErrors(), toArray()));

    expect(toasts.$items()).toEqual([]);
  });
});
