# Authentication and authorization — decisions

Referenced from [CLAUDE.md](../CLAUDE.md).

Two roles. A regular user views rooms and schedules, books free slots, and sees and cancels
their own bookings. An admin does all of that and additionally manages rooms and sees and
cancels any booking.

## One origin

The frontend build is served from the API's static files directory, so a single web app
serves the SPA, the API and the real-time hub. Client-side routing is supported by falling
back to the SPA entry point for unmatched paths. In development the dev server proxies API
and hub paths to the backend, so the browser sees the same single origin locally as in
production.

This removes CORS from every environment — an entire class of deployment failure, in a system
that also has to get WebSockets working — and leaves one deployable artefact that cannot
drift out of version sync with itself.

**Gotcha:** the development proxy entry for the hub path must enable WebSocket forwarding,
or the upgrade is not proxied and the real-time transport silently degrades to long polling.

## Framework identity as the user store

ASP.NET Core Identity with its EF Core store, against the same database. Password hashing,
normalised lookups, lockout on repeated failures and the role tables come maintained by
Microsoft; hand-rolling any of it is a liability with no upside.

Roles living in our own database is what lets us seed a demo admin and a demo user and hand a
reviewer working credentials.

**Note:** Identity's default key is a string holding a GUID, which would make the booking's
user foreign key a long string column and every join a string comparison. Use GUID keys
explicitly — it costs one generic parameter and is awkward to change once the user table has
rows.

## Self-issued bearer tokens

Login validates credentials through Identity, then issues a signed token carrying the user id,
email and role. The API validates the signature. No server-side session state.

Why not cookies, which are genuinely more secure for a browser client — the ticket is not
readable by script, and the framework revalidates it against the user's security stamp,
giving real revocation:

- **The concurrency test stays about concurrency.** Acquiring tokens for many users and firing
  truly parallel requests needs no cookie container and no per-user handler.
- **Stateless validation** — a signature check, no database round trip per request, nothing to
  coordinate when the app scales to several instances.
- **No key ring to operate.** Cookie tickets are encrypted with keys generated at startup and
  persisted to disk, which becomes a real concern across instances. A signing key comes from
  configuration and is shared across instances by construction.

This was the closest call in this document. Also rejected: the framework's built-in identity
endpoints (no role support, and its tokens are not the format we need), an external identity
provider such as Entra (role assignment would live in a portal rather than the repository, so
a reviewer could not run the system), and a full OIDC authorization server or a
backend-for-frontend proxy (machinery without a purpose at one client and two roles).

**Accepted risk:** the token is reachable from JavaScript, so a cross-site scripting flaw
would expose it. Mitigated by holding it in memory only — never in browser storage — and by a
bounded lifetime. A page refresh therefore requires logging in again.

## Long-lived token, no refresh

One token lasting a working day. No refresh token, no rotation, no revocation list.

Refresh tokens resolve the tension between short expiry and not logging the user out
mid-session. Doing it properly means a token table, rotation so theft is detectable,
revocation on logout, and a client that queues concurrent failures so ten simultaneous
retries do not fire ten refreshes. That demonstrates nothing this task assesses, and a
half-built refresh flow is worse than none.

**Accepted risk, stated plainly:** a token cannot be revoked before it expires. Logout
discards it client-side only. Production would use a short access token plus a rotating
server-side refresh token.

## Policies, not role strings

Authorization attributes name a *capability*; the rule behind it is registered once at
startup. Policy and role names are constants, so a typo fails at compile time.

Runtime behaviour is identical to naming the role on the attribute. The difference is what
the source records. Naming the role keeps the answer and discards the question: endpoints that
are admin-only for entirely different reasons become textually identical. Introduce a role
that may maintain rooms but must not see other people's bookings, and a search returns seven
indistinguishable hits whose intent must be reconstructed one by one — and getting one wrong
exposes private data with no compiler error and no failing test.

With policies the two groups were distinguished when the reason was actually known, and the
same change becomes one line. Policies also compose in ways an attribute string cannot.

## Ownership is checked against loaded data

A regular user may cancel their own booking but not someone else's. That is not a role check,
and an attribute cannot do it: attributes run before the action executes, and therefore before
the row is loaded. At that point the framework knows the caller's role but not who owns the
booking, or whether it exists at all.

Without an explicit check, a request to cancel another user's booking passes the role test —
regular users may cancel bookings — and proceeds to cancel it. This is broken object level
authorization, first on the OWASP API security list.

So the rule is a registered requirement evaluated against the already-loaded entity, invoked
after the row is fetched and before it is acted on. A handler rather than an inline check
because the rule is then written once instead of restated at every endpoint touching a
booking — and omitting it at a new endpoint is a vulnerability with no compiler error and no
failing test.

**Collections are a different mechanism.** A requirement evaluates one loaded entity; it does
not apply to a list endpoint, where loading everything and then filtering would be both slow
and leaky. List endpoints enforce the same rule as a query filter instead. One rule, two
enforcement points, because "fetch one and check" and "fetch only what is permitted" are
structurally different operations.

## Real-time connections

Browsers cannot set an authorization header on a WebSocket handshake, so the real-time client
sends the token as a query parameter and the server is configured to read it from there.
Without this, an authorized hub rejects every connection with no obvious cause.

The server only accepts a token from the query string on hub paths. A query string is not a
safe place for a credential in general — it lands in logs and referrers — so this is scoped
narrowly to where it is the protocol-sanctioned mechanism.

## Secrets

The signing key is never committed. Locally it comes from user secrets; in the deployed
environment from application configuration. Seeded demo passwords are development-only and
are overridden in deployment.

## Deliberately out of scope

Refresh tokens and rotation, email confirmation, password reset, multi-factor, external or
social login, permissions finer than two roles, audit logging. Each is straightforward to add
on top of this design and none is required by the task.
