# Slot model — decisions

Referenced from [CLAUDE.md](../../CLAUDE.md).

This file records what a bookable slot *is*. The guarantee that a slot is booked exactly once
is a separate decision, recorded in [concurrency.md](../concurrency/concurrency.md). That
decision revised one section of this file, which is noted where it happened.

## Three tables: room, slot, booking

A room owns rows in a slot table, and a booking references one slot. The three are cleanly
divided by what they mean: the room holds the *rules* of its grid, a slot is one *calendar
row* produced by those rules, and a booking is a *claim* on one slot.

Why this and not deriving the grid from the room's opening hours at read time, which was the
strongest alternative:

- **"Is this a real slot of this room?" is answered by the database.** A foreign key proves
  both that the slot exists and that it belongs to the room the caller named. Deriving the
  grid moves that question into handler code, where it is one method and one unit test — but
  a method that can be forgotten at a new endpoint, with no compiler error.
- **The booking guarantee rests on a single column.** Whether that column is claimed is the
  whole of the mechanism, and the graded requirement is the thing a reviewer should be able to
  read in one line.
- **A slot has an identity that the API and the hub can name.** Booking is a request against a
  slot id rather than against a room plus a timestamp, and a real-time message can say which
  slot changed without restating the coordinates that identify it.
- **Per-slot facts have an obvious home.** A slot closed for maintenance, or reserved for a
  department, is a column on a row that already exists. Without slot rows, every such fact
  needs its own table of exceptions to a computed grid.

Also rejected: **user-chosen start and end times with overlap checking.** Overlap is not an
equality, so no unique index can express it — the guarantee would need serializable range
locks or an application lock, with the blocking and deadlock handling that follow. The task
specifies a fixed set of bookable slots, so this is a weaker guarantee bought with more
machinery.

**Accepted trade-off:** rows do not create themselves. Five questions follow from that, and
each is answered below. Left unanswered they produce a deployment whose schedule silently
empties, duplicate rows that defeat the booking guarantee, or deleted meetings. Answered they
cost roughly sixty lines.

## The slot row holds the claim

**Revised by [concurrency.md](../concurrency/concurrency.md).** This section originally said the
opposite — that a slot row is inert calendar data carrying no booking state, and that a slot is
booked precisely when a live booking row references it. The concurrency decision reversed it,
and the original reasoning is kept below because it names a real cost that is now being paid
deliberately rather than avoided.

A slot row carries `CurrentBookingId`: the booking that currently holds it, or null. It also
carries a `rowversion`. That column is what a booking request claims, and the claim is the
mechanism by which exactly one request wins — an optimistic concurrency mechanism needs a row
to update, and this is the row.

**What the original decision was protecting, and what it costs now.** A claim on the slot
stores one real-world fact — *this room is taken at ten* — in two places: the slot's claim and
the bookings table. Kept in agreement by hand across booking, cancellation and every path added
later, two copies of one fact drift; and because the schedule read consults only the claim, a
drift would show the wrong availability to every viewer with nothing detecting it. That failure
is silent, and it is a failure of exactly the property this system exists to demonstrate. The
concern was correct. It is answered in concurrency.md — one write path, a conditional release, a
backstop index, and a test asserting the two agree — rather than dismissed.

**What it buys.** The schedule read is a single-table query with no join to bookings, and the
"spare the booked ones" predicates elsewhere in this file become a null check on one column
instead of a join.

**Accepted trade-off:** slot rows are no longer inert, so they are no longer unconditionally
safe to delete and recreate. The rule-change policy below already accounted for this by sparing
future slots that are booked; that predicate is now `CurrentBookingId IS NULL`, and it is
load-bearing rather than a formality.

## Slots are generated on demand, when a schedule is read

Requesting a room's schedule for a date generates that date's rows if they are missing, then
returns them. Generation is bounded to a booking window of a fixed number of days from today;
a date outside the window returns "not open for booking" and writes nothing.

The alternative shapes both fail in ways that matter here. **Generating at room creation** puts
a fixed number of days into the table once and never adds more, so the room becomes
permanently unbookable the day the last generated date passes — no error, no log, just an
empty schedule. **A background service topping up daily** is the conventional answer and the
right one for a system with an operator, but it has to actually keep running: an Azure App
Service without Always On idles its process out and the timer stops, and a scaled-out
deployment runs the same top-up on every instance at once.

Generating at read cannot run dry, because any date inside the window materialises at the
moment it is asked for. It needs no always-on process, survives idling and scale-out, and
self-heals if rows are ever lost. A background top-up can be added later as an optimisation
without changing the model.

**Accepted trade-off:** a read performs a write on first access to a date. It is one bounded
insert on a cold date and nothing at all thereafter.

**Note:** the room still owns the rules that say where slot boundaries fall, and generation
calls them. Materialising slots does not remove the grid logic — it adds a table in front of
it. The rules are the single definition of the grid and nothing else may compute boundaries.

## Changing a room's rules: hours regenerate, slot length is restricted

**Opening and closing times may be changed at any time.** Every future slot row for that room
that has no live booking is deleted, and nothing is regenerated eagerly — the next read rebuilds
from the new rules. Future slots that *are* booked are left exactly as they are and continue to
display. Nobody's meeting moves or disappears, and the schedule shows the old and new grids
side by side until those bookings pass or are cancelled.

**Slot length may be changed only while the room has no future bookings**, and the request is
refused with a conflict otherwise, naming how many bookings stand in the way. This is the one
change that cannot be reconciled: an hour-long booking left standing under a new half-hour
grid overlaps two new slots, and two people could then hold overlapping time through slots
that are each individually booked once. Refusing is the only answer that keeps the guarantee
true.

## Losing a race to generate a slot is a success

A unique index on the slot's room and start time makes duplicate rows impossible. Without it,
two concurrent first reads of the same date, two instances running the same top-up, or a
re-run after a partial failure each produce two rows for one moment in time — and two people
then book "the same" slot legally, each against a different row. That would defeat the booking
guarantee through a side door that the booking code itself is powerless to close.

When generation loses that race the insert fails on the index, and the failure is **swallowed**:
the row that was wanted now exists, which is the outcome that was being aimed at.

**Gotcha:** the same duplicate-key error number appears on the bookings table, where it means
something else entirely and is handled a third way. Losing a race to *create a slot* means the
slot now exists, which is success, so it is swallowed here. On the bookings table the error can
only come from the backstop index, which means a code path bypassed the booking mechanism — a
bug, deliberately left to fail loudly rather than dressed up as a conflict. And a genuine lost
booking race raises no duplicate-key error at all: it is a concurrency-token failure. Three
paths, one error number, three correct handlings — see
[concurrency.md](../concurrency/concurrency.md). Anyone touching one should know the others
exist, or the handling of one will eventually be copied onto another.

## Rooms are deactivated, never deleted

Deleting a room removes it from listings by clearing an active flag. The row, its slots and
their bookings remain.

A hard delete cascades through slots into bookings, so one click would destroy several
people's meetings with no record that they existed and no way back. Deactivation keeps the
admin view of past bookings honest and is reversible. As a backstop the slot-to-booking
relationship restricts deletion rather than cascading, so any future code path that attempts a
hard delete fails loudly instead of quietly destroying history.

## Where this meets concurrency

**Decided in [concurrency.md](../concurrency/concurrency.md):** a booking request claims the
slot row by a version-checked update, so exactly one of several simultaneous requests wins and
the rest receive a conflict. A filtered unique index over live bookings sits underneath as a
backstop that makes a second live booking impossible even if the claim is wrong — it is not the
mechanism, and it is not what produces the conflict response.

What this model contributed to that decision: a slot has an identity, so a claim is a write to
one named row; and the grid is a fixed set of discrete slots, so the invariant is an equality on
one column rather than a range overlap, which is what keeps range locks and serializable
isolation out of the design.

## Deliberately out of scope

Per-room time zones and daylight saving — all times are UTC and room hours are UTC wall clock.
Retention of past slots, which at roughly three thousand rows per room per year is not a
problem worth solving. Recurring bookings, bookings spanning several consecutive slots, slots
individually blocked for maintenance, waiting lists, and rooms bookable by more than one party
at once. Each fits this model as an addition rather than a revision, which is part of why it
was chosen.
