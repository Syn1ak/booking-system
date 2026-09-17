import { safeReturnUrl } from './safe-return-url.util';

describe('safeReturnUrl', () => {
  it('follows a same-origin path', () => {
    expect(safeReturnUrl('/rooms/r1?date=2026-09-17')).toBe('/rooms/r1?date=2026-09-17');
  });

  it('refuses anything that could leave the site', () => {
    expect(safeReturnUrl('//evil.example')).toBe('/rooms');
    expect(safeReturnUrl('https://evil.example')).toBe('/rooms');
    expect(safeReturnUrl(null)).toBe('/rooms');
  });
});
