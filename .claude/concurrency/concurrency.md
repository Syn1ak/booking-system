# Concurrency control — decisions

Referenced from [CLAUDE.md](../../CLAUDE.md).

This is the graded centrepiece. The contract: when several requests target the same slot at
effectively the same moment, **exactly one succeeds**, the rest receive a **clear conflict
response**, and neither a silent overwrite nor a server error is ever an outcome. That last
clause is a design constraint, not a nicety — an unhandled duplicate-key exception surfacing
as a 500 leaves the data correct and still fails the requirement.

## The claim is a version-checked update of the slot row

A slot row carries `CurrentBookingId` — the booking that currently holds it, or null — and a
`rowversion` concurrency token. Booking is one `SaveChanges`: insert the booking row and set
the slot's `CurrentBookingId`. EF Core appends the token to the update:

```sql
UPDATE Slots SET CurrentBookingId = @booking WHERE Id = @slot AND Version = @version
```

The winner's update matches one row. Every loser read the same token, so their update matches
zero rows, EF raises `DbUpdateConcurrencyException`, and because both writes ride a single
`SaveChanges`, the loser's booking insert rolls back with it. One call, atomic, no explicit
transaction to manage.

This is not the check-then-write the task disqualifies. The freeness of the slot is not read
and then trusted; it is *asserted in the predicate of the write*, so there is no window
between the decision and the claim for another request to occupy.

Why this and not the alternatives, each of which is a defensible answer to the same problem:

- **A unique index on the booking's slot id, inserting unconditionally and translating the
  duplicate-key failure into a conflict.** The strongest rival, and the mechanism most of the
  field reaches for. Rejected as the *primary* mechanism because the conflict is then
  discovered by provoking a database error and matching a provider-specific error number, and
  because the same failure already means the opposite thing one folder away (see the backstop
  section). It remains in the schema as a backstop, which is where it earns its keep.
- **A pessimistic row lock** — `SELECT ... WITH (UPDLOCK, HOLDLOCK)` and then insert inside a
  transaction. Correct, and the conventional answer where a claim spans several statements.
  Rejected because our claim is a single statement, so the lock protects a window that does
  not exist, and it buys blocking, deadlock handling, a transaction spanning read and write,
  and raw SQL for the hints because EF Core has no first-class support for them. Its
  correctness also depends on `HOLDLOCK` being present, which a reviewer must reason about
  rather than read.
- **Serializable isolation.** The only mechanism that can enforce a *range* invariant such as
  "no overlapping bookings". Not needed: a fixed grid of identified slots makes the invariant
  an equality on one column. Serializable without lock hints is also measurably worse than
  useless here — it converts contention into deadlocks.
- **A distributed lock with a TTL**, in Redis or via `sp_getapplock`. The right answer when a
  slot must be *held* while something slow happens — a payment, a human deciding. There is no
  such step here, so there is nothing to hold the slot for, and the lock would be acquired and
  released inside one request.
- **The same conditional update without a token** — `WHERE CurrentBookingId IS NULL`, checking
  rows-affected. Nearly identical in behaviour, and a legitimate simplification. The token is
  preferred because it is the mechanism the task names, EF Core supports it directly, and a
  typed exception translates to a response more legibly than a rows-affected integer that a
  future edit could stop checking with no compiler error.

**Accepted trade-off, stated plainly:** availability is now *stored* rather than derived. "Is
this slot taken" is answered by `Slot.CurrentBookingId`, and the same fact is implicit in the
bookings table. Two places holding one fact can drift, and drift would show the wrong
availability with nothing detecting it. Everything in "Keeping the stored claim honest" below
exists to pay that cost down.

**Note:** `rowversion`'s usual drawback — spurious conflicts when an unrelated column on the
row changes — does not apply. Nothing else about a slot is ever updated: an opening-hours
change deletes and regenerates slot rows rather than editing them, so the only write that
moves a slot's token is a claim or a release.

## What the token protects, and what it does not

The token protects **that row against concurrent writers**. It does not express the invariant
"a slot has at most one live booking" anywhere the database can enforce it. Nothing stops a
future code path from inserting a booking without going through the slot update — a new
endpoint, an admin tool, a data fix, a mistake — and the result would be two live bookings for
one slot with no error anywhere.

This is the one structural weakness of the choice, and it is the reason for the next decision.

## A filtered unique index as a backstop — explicitly not the mechanism

A filtered unique index on the booking's slot id, over live bookings only:

```sql
CREATE UNIQUE INDEX IX_Bookings_SlotId_Live ON Bookings(SlotId) WHERE CancelledAtUtc IS NULL
```

Filtered because cancellation preserves history: a slot may accumulate any number of cancelled
bookings and at most one live one.

**The division of labour is exact, and the doc says so because the alternative is that someone
later deletes one of the two as redundant:**

- The **token** produces the 409. It is the mechanism, and it is what the concurrency test
  exercises.
- The **index** produces nothing in normal operation. It exists so that a code path which
  bypasses the token fails instead of double-booking. If it ever fires, that is a bug becoming
  visible, not a conflict — so it is **not** translated into a 409. It surfaces as a 500, which
  is the honest answer: the request did not lose a race, the system is wrong.

**Gotcha:** SQL Server raises 2601/2627 for this index, the same numbers `SlotGenerator`
deliberately swallows when it loses a race to create a slot row. Three code paths, one error
number, three correct handlings — swallow (creating a slot that now exists is success), 409
(not used here), and let it fail loudly (a bypassed token). Anyone touching one should know the
other two exist, because the failure mode of this codebase is copying one path's handling onto
another.

## Losing a claim is not retried

EF Core's documented way to resolve a concurrency conflict is to refresh the original values
and retry. That is right for last-write-wins edits and **wrong for a claim**, so it is recorded
here as the obvious wrong turn: retrying a lost booking can only succeed if the winner cancels
in the interim, which is not what the caller asked for, and a retry loop on a contested slot is
a loop that spins until someone gives up.

A lost claim fails fast with a conflict. Transient faults and deadlock victims (error 1205) are
a different category and *are* retried — by the connection resiliency policy, not by the
booking handler. The two must not be merged: one means "the slot is taken", the other means
"ask again and it may work".

**Gotcha for whoever turns on `EnableRetryOnFailure` for Azure SQL:** the retrying execution
strategy refuses user-initiated transactions, so any code opening one explicitly must wrap it
in `Database.CreateExecutionStrategy().ExecuteAsync(...)`. The booking path is safe by
construction — it uses one `SaveChanges` and opens no transaction — but `UpdateRoom` already
does, so this bites the existing code on the day the deployed connection string enables
retries.

## Releasing a claim is conditional on still holding it

Cancellation clears the slot's claim, and that write is conditional on the claim still being
this booking's:

```sql
UPDATE Slots SET CurrentBookingId = NULL WHERE Id = @slot AND CurrentBookingId = @booking
```

The predicate is not optional. Without it, a cancellation racing a rebooking of the freed slot
clears the *new* holder's claim: two users then believe they hold the slot, one of them
correctly. That is the silent overwrite the requirement forbids, arriving through the
cancellation path rather than the booking path — which is exactly why it would be missed.

## Cancellation preserves history, and is idempotent

Cancelling sets `CancelledAtUtc` rather than deleting the row. The admin view of every user's
bookings has to include bookings that were cancelled, and the backstop index filters on
precisely this column. A timestamp rather than a status enum, because it also answers *when*
and the filter is a null check.

Cancelling an already-cancelled booking **succeeds**. The caller's intent is that the booking
not stand; it does not stand. Reporting a conflict for a retried or double-clicked cancellation
would be a failure the user can do nothing about.

The booking row itself also carries a `rowversion`, so two simultaneous cancellations of one
booking cannot both apply. The second is either idempotent success or a conflict, never a
partially applied pair of writes.

## Booking a slot you already hold is not a conflict

If the live booking for the slot turns out to be the caller's own, the response is 200 with
that booking, not 409. A double-click or a retried request has already achieved what it asked
for, and "you are in conflict with yourself" reads as a bug. This costs one extra read, on the
losing path only.

Per-request idempotency keys are the general solution to duplicate submission and are out of
scope: they solve a problem this narrow case already covers.

## The conflict response

409 with `ProblemDetails`, consistent with every other slice, worded about the **slot**: that
slot was just booked by someone else. Not about tokens, versions or rows affected. The
requirement is a *clear* conflict response, and the mechanism's vocabulary is clear to us and
meaningless to the person who clicked.

## Validation runs first and is not the guarantee

Before attempting the claim the handler checks that the slot exists, that its room is still
active, that the slot has not already started, and that it falls inside the booking window.
These are ordinary request validation and produce 400 or 404.

**None of them is the guarantee**, and the file says so where they are written, because a
reader who mistakes them for the protection will eventually "optimise" the token away. In
particular there is deliberately **no pre-check that the slot is free**: it would add a round
trip, it could not be trusted by the time the write ran, and its presence invites exactly that
misreading.

## What the schedule shows

Each slot in a schedule response carries `isBooked` and `isMine`. Never the holder's identity,
name or email: a regular user may not see other users' bookings, and the schedule is the
endpoint where that would leak by accident. Admins get the full picture from the bookings
endpoints, where it is the point.

Because the claim is stored on the slot row, this costs no join — the schedule read stays a
single-table query, which is the compensation for storing the fact twice.

## Keeping the stored claim honest

Four things hold the slot's claim and the bookings table in agreement:

1. **One write path.** Booking and cancellation are the only operations that touch
   `CurrentBookingId`, and each writes both rows in one atomic `SaveChanges`.
2. **The conditional release** above, so a release cannot clear someone else's claim.
3. **The backstop index**, which makes a second live booking impossible even if the claim is
   wrong.
4. **An integration test asserting the two agree** after a book / cancel / rebook cycle. The
   invariant is stated once as a test rather than trusted to review.

## Proving it: the concurrency test

Requirement 6 wants simultaneous booking *requests*, so the headline test drives HTTP: N
authenticated clients fire at one slot and the assertions are **exactly one 201, N−1 409s, zero
5xx responses, and exactly one live booking row**. The zero-5xx assertion is part of the
requirement, not extra credit.

**A parallel test is not by itself evidence, and this was verified the hard way** during slot
generation: eight attempts awaited directly, or started with `Task.Run`, never collided, because
xUnit installs a synchronization context that schedules continuations onto a bounded set of
threads and the first attempt finished before the rest reached their write. That suite passed
with the mechanism deleted, which is the definition of a test that proves nothing.

So the same two-test shape applies here:

- **Outcome:** dedicated threads (`TaskCreationOptions.LongRunning`) released by a shared gate,
  asserting the counts above.
- **Code path:** a deterministic race — a rival claim committed from a second connection inside
  a `SaveChangesInterceptor`, in the window between the handler's read of the token and its
  write. With the token removed, exactly this test fails.

**The standard to hold:** a concurrency test that still passes when the mechanism is removed is
not a test. Both tests are checked against that, deliberately, and the check is recorded.

## What this changes elsewhere

- **[scheduling.md](../scheduling/scheduling.md) is revised, not merely extended.** Its
  decision that a slot row holds no booking state is reversed by this one. The reasoning that
  produced it was sound and its concern — one fact in two places, drifting silently — is real;
  it is answered above rather than dismissed.
- **Two deferrals in the scheduling plan get cheaper.** Sparing booked slots when a room's
  hours change becomes `WHERE CurrentBookingId IS NULL`, and the "no future bookings" guard on
  a slot-length change becomes a count over the same column. Both were going to need a join.

## Deliberately out of scope

Holds with expiry and any reserve-then-confirm flow, payment, per-request idempotency keys,
per-user booking limits, bookings spanning several consecutive slots, and detecting that one
user has two bookings at the same time in different rooms. Each is an addition to this model
rather than a revision of it.
