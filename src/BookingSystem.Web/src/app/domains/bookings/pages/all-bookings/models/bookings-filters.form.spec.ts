import { BookingsFiltersFormHandler, RANGE_REVERSED } from './bookings-filters.form';

describe('BookingsFiltersFormHandler', () => {
  it('reads absent query parameters as "everything, cancelled included"', () => {
    expect(BookingsFiltersFormHandler.toCriteria({})).toEqual({
      roomId: null,
      from: null,
      to: null,
      includeCancelled: true,
    });
  });

  it('writes only what differs from the default back to the URL', () => {
    const form = new BookingsFiltersFormHandler();

    form.setFromCriteria({ roomId: 'r1', from: '2026-09-01', to: null, includeCancelled: false });

    expect(form.toQueryParams()).toEqual({
      roomId: 'r1',
      from: '2026-09-01',
      to: null,
      includeCancelled: 'false',
    });
  });

  it('refuses a range that ends before it starts', () => {
    const form = new BookingsFiltersFormHandler();

    form.patchValue({ from: '2026-09-20', to: '2026-09-10' });

    expect(form.hasError(RANGE_REVERSED)).toBe(true);
  });
});
