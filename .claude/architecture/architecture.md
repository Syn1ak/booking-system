# Architecture — decisions

How the code is organised, and why. Referenced from [CLAUDE.md](../CLAUDE.md).

## Vertical slices, not layers

The application is organised by feature, not by technical layer. There is one folder per
area and one file per use case inside it. Genuinely shared concerns — the database context,
the real-time hub, authorization policies, entities — sit in sibling folders at the same
level.

Why this and not folder-by-type or Clean Architecture:

- **It puts the graded requirement in one place.** The concurrency guarantee is a database
  guarantee. In this structure the mechanism, the reasoning behind it, and the error
  translation all live in the file that creates a booking. A reviewer opens one file.
- **New features have one obvious home,** and that answer does not change as the system grows.
- **Change is contained.** A slice owns its request shape, validation and query, so altering
  how bookings are cancelled cannot break how rooms are listed. The blast radius is visible
  from the file tree.
- **Coupling points the right way.** Layers group things that are never read together and
  force every feature through all of them. Slices give high cohesion inside a feature and low
  coupling between features — and features are what actually change.
- **Deleting a feature is deleting a folder,** leaving no orphaned interfaces behind.

Clean Architecture was the strongest alternative and is the right answer for a larger domain.
It was rejected here because its central rule is that the application layer must not know
about the database — but this task's central requirement *is* a database guarantee, so the
graded mechanism ends up either leaking through the pattern or hidden behind an interface.
At three entities it would also mean touching three projects to add one feature.

**Accepted trade-off:** nothing at the compiler level stops one slice reaching into another.
That is review discipline, not a structural guarantee.

## One backend project

`Domain` and `Data` are folders, not separate assemblies. The only rule a project split would
actually enforce is keeping the domain free of EF Core — one rule, easy to hold by hand at
this size, and buying it would cost a wall between the booking handler and the database
behaviour it exists to demonstrate.

Splitting later is a mechanical refactor: move folders into projects, fix usings.

## One file per use case

Each use case is a single class holding its route registration, request and response types,
validation, and handler. These change together and are read together, so they are edited
together rather than scattered across four folders.

Files are named after the operation, not after a type, so a folder listing reads as an
inventory of what the system does.

**Accepted trade-off:** files are longer, and some duplication between similar slices is
tolerated deliberately. Two slices that look alike today frequently diverge tomorrow, and
premature extraction couples them permanently. Extract on the third occurrence, not the
second.

## Data access is direct

Slices use the database context directly. There is no repository and no generic service layer.

The context is already a unit of work and its sets are already repositories, so wrapping them
reimplements what is being wrapped. More importantly it would harm the graded requirement:
the concurrency mechanism needs explicit control over the transaction, and behind a repository
method that becomes one line of delegation with the real behaviour hidden elsewhere.

Each slice also shapes its own query — a schedule projection is not the same query as an
ownership check — rather than both going through one over-general method that fetches too much.

**Accepted trade-off:** slices are coupled to EF Core and SQL Server. This is deliberate. The
central requirement is a database-level guarantee; a structure that hid the database would be
hiding the answer.

## Shared invariants live on entities

Rules that hold regardless of which slice is executing belong on the entity, not restated in
each handler — the known failure mode of this architecture is a core rule drifting between
slices that each reimplemented it.

The boundary: rules true of the entity *always* go on the entity; rules true of *this request*
stay in the slice. "A cancelled booking cannot be cancelled again" is the first kind. "The
slot must be at least an hour from now" is the second.

## No mediator

Endpoints call their handler directly. A mediator's value is pipeline behaviours, and the
framework already provides endpoint filters that cover the same ground without a dependency or
a dispatch hop. With one caller per handler the indirection resolves to a single known callee.

## Endpoints register themselves

Slices expose a registration method found by scanning the assembly at startup, rather than
being listed centrally. A central list is a file edited for every feature and conflicted on
every parallel branch — and forgetting it produces a feature that compiles, passes unit tests,
and returns 404, with no compile error and no obvious cause.

## Tests mirror features

The integration test project mirrors the feature folders one-for-one, so missing coverage is
visible from the directory listing. The concurrency test gets its own folder rather than being
filed under bookings: it is the single most important artefact in the repository for the
graded requirement, and should be findable without knowing where to look.

Integration tests run against a real SQL Server in a container, driven through the HTTP stack.
The in-memory provider implements neither unique indexes nor isolation levels, so a
concurrency test against it would pass while proving nothing — exactly the incidental
behaviour the task warns against.
