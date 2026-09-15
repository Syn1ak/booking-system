# Slot model — decisions

Referenced from [CLAUDE.md](../../CLAUDE.md).

This file records what a bookable slot *is*. The guarantee that a slot is booked exactly once
is a separate decision and is **not yet made**; where this model constrains it is noted at the
end.

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
- **The booking guarantee will rest on a single column.** A unique index on the booking's slot
  id is as small as this mechanism gets, and the graded requirement is the thing a reviewer
  should be able to read in one line.
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

## The slot row holds no booking state

A slot row is inert calendar data: which room, when it starts, when it ends. It carries no
"is booked" flag. A slot is booked precisely when a live booking row references it, and the
schedule projection asks that question directly.

This is the smallest decision here and the one most worth getting right. A flag on the slot
would store one real-world fact — *this room is taken at ten* — in two places, kept in
agreement by hand across booking, cancellation, and every path added later. The schedule read
would consult only the flag, so a drift between the two would show the wrong availability to
every viewer with nothing anywhere detecting it. The failure is silent, and it is a failure of
exactly the property this system exists to demonstrate.

It also keeps slot rows safe to delete and recreate, which is what makes the rule-change
policy below workable: inert rows carry nothing that could be lost.

**Accepted trade-off:** every schedule read joins to bookings. The index that will enforce the
booking guarantee already serves that join, so the cost is nil.

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

**Gotcha:** this is the same duplicate-key error that booking will later translate into a
conflict response, handled in the opposite way. The distinction is not arbitrary. Losing a
race to *create* a slot means the slot exists, which is success. Losing a race to *book* one
means it belongs to someone else, which is a conflict. Anyone touching either path should know
both exist, or the handling of one will eventually be copied onto the other.

## Rooms are deactivated, never deleted

Deleting a room removes it from listings by clearing an active flag. The row, its slots and
their bookings remain.

A hard delete cascades through slots into bookings, so one click would destroy several
people's meetings with no record that they existed and no way back. Deactivation keeps the
admin view of past bookings honest and is reversible. As a backstop the slot-to-booking
relationship restricts deletion rather than cascading, so any future code path that attempts a
hard delete fails loudly instead of quietly destroying history.

## Where this meets concurrency

Recorded here only as what this model leaves open, not as a decision.

The booking guarantee has a single natural key under this model: the slot id on the booking
row, unique across live bookings. Because cancellation must preserve history rather than
delete rows, that uniqueness has to hold over non-cancelled bookings only, which points at a
filtered unique index. Both the pessimistic and optimistic mechanisms remain available on top
of it. Nothing here forecloses that choice.

## Deliberately out of scope

Per-room time zones and daylight saving — all times are UTC and room hours are UTC wall clock.
Retention of past slots, which at roughly three thousand rows per room per year is not a
problem worth solving. Recurring bookings, bookings spanning several consecutive slots, slots
individually blocked for maintenance, waiting lists, and rooms bookable by more than one party
at once. Each fits this model as an addition rather than a revision, which is part of why it
was chosen.
