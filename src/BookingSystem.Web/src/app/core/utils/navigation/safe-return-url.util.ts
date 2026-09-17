/**
 * Only same-origin paths are followed. `//evil.example` is protocol-relative and would leave
 * the site, so a return URL must start with exactly one slash.
 */
export function safeReturnUrl(returnUrl: string | null | undefined, fallback = '/rooms'): string {
  return returnUrl && /^\/(?!\/)/.test(returnUrl) ? returnUrl : fallback;
}
