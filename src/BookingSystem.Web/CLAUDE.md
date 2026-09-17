# CLAUDE.md — frontend

The browser half of the meeting room booking system: an Angular single-page app styled with
Bootstrap and ng-bootstrap, built into the API's `wwwroot` and served from the API's origin.
The repository-level [CLAUDE.md](../../CLAUDE.md) still applies — atomic commits, rationale in
commit messages rather than comments, decide before implementing.

## Commands

All from `src/BookingSystem.Web/`, with the API running (`dotnet run --project src/BookingSystem.Api`
from the repository root) for anything that talks to the server.

```
npm ci            # once per clone
npm start         # dev server on http://localhost:4200, proxies /api and /hub to the API
npm run build     # production build into ../BookingSystem.Api/wwwroot
npm test          # unit tests (Vitest), add -- --watch=false for a single run
npm run lint      # ESLint (angular-eslint)
npm run format    # Prettier
```

## Contracts this app implements

The server's decisions are fixed; the client implements them and does not revisit them. Read
the relevant file before touching that area.

| Area | Server decision | What it means here |
|---|---|---|
| Auth | [auth.md](../../.claude/auth/auth.md) | Token in memory only — never web storage. Refresh logs out; login keeps `returnUrl`. UI asks capabilities (`canManageRooms`), never role strings. |
| Concurrency | [concurrency.md](../../.claude/concurrency/concurrency.md) | 409 on booking is a normal outcome: friendly message and refetch. 200 on booking is success. No optimistic "booked". Never show who holds a slot. |
| Real-time | [realtime.md](../../.claude/realtime/realtime.md) | Every slot write goes through the merge utility. Join the room *before* fetching; buffer events until the snapshot lands. Reconnect re-joins **and** refetches. Never pin the transport. |
| Scheduling | [scheduling.md](../../.claude/scheduling/scheduling.md) | All times are UTC and shown labelled as UTC. Deleting a room is a deactivation. |

The frontend plan with the full step list is `docs/frontend-plan.md` (local, not committed).

## Architecture rules (non-negotiable)

Follow them without being asked; when a change would violate one, say so instead of silently
deviating.

1. **data-access / view split.** Business logic lives in `data-access/` (facades, services,
   guards, resolvers). `view/` is presentation only.
2. **No sibling dependencies.** Never import between sibling pages, sibling features or sibling
   domains. Dependencies flow one way: pages → `domains/<d>/view|data-access` → `shared/ui` and
   `core`. `layout` → `core` and `shared/ui`.
3. **Share by extracting one level up.** Two siblings need the same code → move it to their
   nearest common parent (page → domain → `shared/features` → `core`). Never a lateral import.
4. **Lazy-load everything.** `loadComponent` / `loadChildren` in a `*.routes.ts`; page
   components use `export default`; `loadChildren: () => import(...).then((r) => r.ROUTES)`.
5. **Standalone components, `OnPush`** always.
6. **`inject()`**, never constructor injection.
7. **`$` prefix for signals**, **`$` suffix for observables** (`$slots`, `slotChanged$`,
   `getRooms$()`).
8. **`shared/ui` is dumb** — inputs and outputs only; never injects state or services.
9. **`core/` is headless** — services, interceptors, guards, utilities, DTOs; no components, and
   never imports from `domains/` or `layout/`.
10. **The backend is the source of truth.** After a mutation, refetch. The one qualified case: a
    booking response carries the slot's sequence and is merged like a snapshot instead.

Naming: interfaces for wire DTOs are `I`-prefixed (`IRoom`); client-side types are `T`-prefixed
(`TSlotState`). Files are `name.kind.ts` (`room-list.component.ts`, `rooms.client.ts`,
`room-schedule.facade.ts`, `slot-merge.util.ts`). Class members follow the order in the code
style guide: constants → injected → inputs/outputs/queries → signals → properties → constructor
→ lifecycle → getters → public → private.

## Structure

```
src/app/
  core/          headless: entities (DTOs), services/api clients, session, realtime hub client,
                 notifications, errors, guards, interceptors, utils (form, date), providers
                 (ng-bootstrap adapters), core.provider.ts
  layout/        main-layout (navbar, user menu, connection indicator, toasts), auth-layout
  domains/
    auth/        pages: login, register
    rooms/       pages: room-list, room-schedule, manage-rooms · view: room-card
    bookings/    pages: my-bookings, all-bookings · view: booking-status-badge
  shared/ui/     page-header, empty-state, skeleton, confirm-dialog, pipes
src/styles/      Bootstrap theme and project partials
```

**Gotcha:** provide ng-bootstrap adapters (such as `IsoDateAdapter`) on the component that uses
the widget, not in `core.provider.ts`. Registering one app-wide pulled the whole datepicker into
the initial bundle. For the same reason `@microsoft/signalr` is imported lazily by the hub
connection factory; keep value imports of it out of eagerly loaded files.

Admin is a capability, not a domain: manage-rooms sits in `rooms`, all-bookings in `bookings`,
mirroring `Features/Rooms` and `Features/Bookings` on the server.

## Local guideline docs

`.claude/` in this directory holds architecture guidelines and skills copied from another
project. It is git-ignored, so these links work only on a machine that has the copy; the rules
above are the committed summary. Read the long form before you:

| Read this | Before you |
|---|---|
| [structure/general-structure.md](.claude/architecture/structure/general-structure.md), [directory-structure.md](.claude/architecture/structure/directory-structure.md), [base-structure.md](.claude/architecture/structure/base-structure.md) | create files, or decide page vs feature vs shared/feature vs core |
| [one-level-up-rule/index.md](.claude/architecture/one-level-up-rule/index.md) | share code between two existing siblings |
| [dependency-graph/dependency-graph.md](.claude/architecture/dependency-graph/dependency-graph.md) | add an import that crosses layers |
| [routing/routing.md](.claude/architecture/routing/routing.md) | add or change a route |
| [code-styleguide/code-styleguide.md](.claude/architecture/code-styleguide/code-styleguide.md) | write a component or service |
| `angular-code-style` skill | create or edit a component, service, facade, guard, pipe or types file |
| `styling-conventions` skill | touch a template's classes or any stylesheet |
| `forms-and-i18-enums` skill | build a form, an enum-backed select, or server-error display |

**Vocabulary map** — the copied docs were written for a different app:

| Copied docs say | Here it is |
|---|---|
| helpers "exported from `itero-shared`" (`ControlsOf`, `getFormControlsNames`, `applyValidationErrors`, `injectHandleErrors`…) | `core/utils/form/*` and `core/errors/*` |
| Angular Material (`mat-error`, `errorStateMatcher`) | Bootstrap `is-invalid` / `invalid-feedback` and ng-bootstrap widgets |
| ProblemDetails `errors: [{ code, message, property }]`, handlers keyed by `title` | ASP.NET `errors: { Field: ["message"] }`, handlers keyed by **status code** |
| `patterns/` | `shared/features/` (none needed yet) |
| `itero-web/assets/scss/` (utils, components, abstracts) | `src/styles/` (utilities, components, `_variables.scss`, `_theme.scss`) |
| palette utilities (`text-neutral-800`, `bg-purple-100`, `padding-4`, `rounded-md`) and `var(--neutral-700)` | Bootstrap utilities and its CSS variables (`text-body-secondary`, `bg-primary-subtle`, `p-3`, `rounded-3`, `var(--bs-border-color)`) plus `--app-*` variables from `_theme.scss` |
| Angular Material overrides as the one `!important` exception | none: override Bootstrap through its variables |

## Styling

**Compose from existing classes; write custom CSS last.** Stop at the first that applies:

1. A Bootstrap utility (`d-flex`, `gap-3`, `p-4`, `text-body-secondary`, `rounded-4`, …).
2. A project class from `src/styles/` — grep the partials before inventing one.
3. **Extend** the matching partial in `src/styles/` when the style is reusable.
4. Component `.scss` only for genuinely component-specific styles.

Never hard-code colours: use Bootstrap's CSS variables (`var(--bs-primary)`,
`var(--bs-border-color)`) so the palette stays in one place. The app is light-only. Theme variables live in
`src/styles/_variables.scss`. Anything animated sits under
`@media (prefers-reduced-motion: no-preference)`. State is never shown by colour alone —
pair it with an icon or text.

## Comments

Only for what the code cannot say: a non-obvious constraint, a contract from a decision file, a
gotcha that already cost time. Names carry the rest.
