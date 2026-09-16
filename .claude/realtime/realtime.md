# Real-time updates — decisions

Referenced from [CLAUDE.md](../../CLAUDE.md).

Requirement 7: when a slot's booking status changes, everyone currently viewing that
resource's schedule sees it immediately, without refreshing. The transport is fixed by the
task — Azure SignalR Service. What is open is *what is broadcast, to whom, when, and what a
client is allowed to conclude from it*, and those are the decisions recorded here.

The concurrency decision ([concurrency.md](../concurrency/concurrency.md)) constrains most of
them. A booking is a committed database write; a push is a notification that the write
happened. Confusing the two — announcing a claim before it commits, or letting a failed push
fail a booking that already stands — would make the real-time feature able to damage the
graded one.

## Push is an optimisation over a pull that stays authoritative

The HTTP schedule endpoint remains the source of truth. A push tells a client that its
snapshot is out of date and carries enough to update it in place; it is never the only way a
client can learn something. Clients refetch on connect, on reconnect, and whenever they cannot
reconcile an event.

This is a deliberate refusal to make delivery guaranteed. Messages can be lost — a dropped
connection, a service restart, a reconnect gap — and the alternative designs that close that
hole (a transactional outbox with a dispatcher, or per-connection replay from a sequence
cursor) each add a process that has to keep running. Scheduling already rejected a background
process for the same reason: an App Service without Always On idles its process out, and a
scaled-out deployment runs one copy per instance. Losing an event here costs a stale cell on
one screen until the next event or reconnect, not a wrong booking — the booking guarantee is
in the database and does not depend on any message arriving.

**The rule that makes this safe:** nothing server-side ever reads a pushed message back. The
push is one-way, view-only, and no write depends on it.

## The event is the public fact only

`SlotChanged` carries the room, the slot, its start, whether it is now booked, and a sequence
number. It does **not** carry who booked it, and there is no variant of the payload that does.

[concurrency.md](../concurrency/concurrency.md) already established that the schedule response
never reveals the holder of a slot to another user: `myBookingId` is set per caller, and the
query is restricted to the caller so another user's booking is never loaded. A broadcast is
exactly where that rule would be lost, because one payload goes to every viewer at once — a
per-user field on a broadcast message is a leak by construction, not by accident.

So the event has no user-specific field at all. A client learns that a slot *is its own* from
its own HTTP response, never from a broadcast. The privacy rule then holds structurally: there
is no code path where a holder's identity could reach the wrong viewer, because the identity
is not in the message.

**Accepted trade-off:** the actor's own client receives the same anonymous event as everyone
else, so it needs a way to recognise its own write. That is what the sequence number below is
for.

## Every event carries a sequence, taken from the slot's rowversion

The slot row already has a `rowversion`, maintained by SQL Server and monotonic across the
whole database. It is exposed — as a number, big-endian — on the schedule response, on the
booking response, and on every event, and it is only ever **compared**, never interpreted.

It does three jobs, which together are why it is worth the extra column in three payloads:

1. **Ordering.** SignalR guarantees ordering within one connection's sends, not between sends
   from different instances through the service. Two events for one slot can therefore arrive
   out of order, and the failure is sticky: a stale "booked" applied after a fresh "free"
   leaves a free slot displayed as taken until something else happens to that slot. A client
   drops any event whose sequence is not greater than what it holds.
2. **Fetch-versus-push races.** A schedule response is a snapshot taken at some moment; an
   event already in flight may describe an earlier one. Same comparison, same rule, no extra
   machinery.
3. **Recognising your own write.** The booking response carries the sequence of the claim it
   wrote. An event with that exact sequence is the caller's own booking, so the client keeps
   its `myBookingId`; an event with a *higher* sequence means the slot changed hands
   afterwards — an admin cancelled it, someone else took it — so the client clears it. Without
   this, an actor either loses the knowledge that a slot is theirs or keeps it after it stops
   being true.

Why the rowversion and not a timestamp or a counter of our own: it already exists, it already
changes on exactly the writes that produce events, and it is the same value the concurrency
token compares — so "the event describes this version of the row" is literally true rather
than approximately true. A wall-clock timestamp would need clocks agreeing across instances; a
new counter column would be a second thing to keep monotonic.

**Gotcha:** a `rowversion` is eight bytes, and a JSON number is a double past the browser's
2^53. The value is a per-database counter starting near zero, so this is not reachable in
practice, but a client must compare the numbers and never do arithmetic on them.

**Gotcha:** the cancellation path releases the claim with `ExecuteUpdate`, which does not bring
the new rowversion back. The new value is read inside the same transaction. Booking needs no
such read — `SaveChanges` already refreshes the token it just checked.

## One group per room, not per room and date

A viewer joins `room:{roomId}` when it opens a room and leaves when it closes it. Slot events
carry their start time, so a client viewing a different date ignores them in a line of code.

Per-room-and-date groups were the alternative, and are tempting because every message would
then be relevant to every member. They were rejected because grid changes are inherently
room-wide: changing a room's opening hours deletes and regenerates future slot rows across
*every* date in the window, and SignalR cannot broadcast to a prefix of group names. Serving
that would mean each viewer joining two groups for one screen, rejoining one of them on every
date navigation, and the server agreeing with the client on a date format inside a group name.
That is more moving parts than the client-side date check it replaces.

**Accepted trade-off:** a viewer receives events for dates it is not looking at. The volume is
bounded by booking activity in a single room.

## A grid change is an invalidation, not a diff

Changing a room's hours or slot length, and deactivating a room, publish `ScheduleReset` for
the room: *everything you know about this room is stale, fetch it again*. No slot-level events,
no list of what was deleted.

Diffing a regenerated grid is real work — slot rows are deleted and recreated with new ids, so
the diff is "these thirty slots no longer exist and these thirty-two do" — to save one request
on an operation that happens rarely and is initiated by an admin, not by the people watching.
A refetch is one call to an endpoint every client already uses on open.

Deactivation uses the same event rather than one of its own: the client refetches, gets a 404,
and closes the room. One event, one client rule.

## Published after commit, and the publisher never throws

The notifier is called after the transaction commits, never inside it. Publishing first would
announce a booking that can still roll back — every viewer showing a slot as taken that is in
fact free, with nothing to correct it, which is the silent-overwrite failure arriving through
the view layer.

**Gotcha, and it is load-bearing:** `BookSlot`, `CancelBooking` and `UpdateRoom` run their
bodies inside `Database.CreateExecutionStrategy().ExecuteAsync(...)`. An exception escaping the
notifier inside that body would be handled as a failed attempt, and the strategy would re-run a
booking that has already committed. So the notifier catches and logs its own failures and
returns normally. The HTTP response is the authoritative outcome; a viewer that missed the push
is one refetch behind, whereas a retried commit is a second booking.

This also means the deployed dependency on Azure SignalR cannot take the API down with it: the
service being unreachable degrades the system to manual refresh.

## One notifier, not a publish call per slice

The hub, its client interface and a `ScheduleNotifier` live in a `RealTime/` folder alongside
the other shared concerns. Slices call the notifier; they do not build messages.

This is the third-occurrence rule from [architecture.md](../architecture/architecture.md)
being met rather than pre-empted: booking, cancellation and the two room changes are four call
sites for one wire contract that a browser has to agree with exactly. Duplicating the payload
shape across four slices makes a client-visible contract something you change in four places
and can get wrong in one.

The hub itself is strongly typed (`IHubContext<ScheduleHub, IScheduleClient>`), so the method
names the browser subscribes to are compile-time symbols on the server rather than strings.

## The hub authenticates; it does not authorize further

The hub requires an authenticated connection. Joining a room's group requires nothing beyond
that.

That is not a gap. Every authenticated user may already read any active room's schedule over
HTTP, and the event carries strictly less than that response does — no holder, no booking id,
nothing a member could not fetch. Adding a per-group check would suggest the group boundary is
a privacy boundary, which it is not; the payload is where privacy is enforced, and that is
recorded above so it is not moved.

Browsers cannot set an `Authorization` header on a WebSocket handshake, so the token arrives
as a query parameter on hub paths only — decided and scoped in [auth.md](../auth/auth.md).

**Gotcha:** group membership does not survive a reconnect. A client that reconnects must
re-join its room *and* refetch the schedule, because events during the gap are gone. Rejoining
without refetching leaves a permanently stale view that looks healthy.

## Azure SignalR when configured, self-hosted otherwise

`AddSignalR()` always; `.AddAzureSignalR()` only when a connection string is present. The
deployed app sets it and therefore satisfies requirement 7 through the service; local
development and the integration tests run the same hub in-process.

The alternative — requiring the service everywhere — would mean a reviewer cannot run the
repository without an Azure subscription, and the test suite would depend on a remote service
to assert a local contract. The hub code, the groups, the payloads and the client are identical
in both modes; what differs is who holds the connections.

Using the service is also what makes scale-out work without a backplane or sticky sessions,
which is the reason to want it beyond the task naming it.

**Accepted trade-off, stated plainly:** the automated tests exercise the hub, not Azure SignalR
Service. They prove the contract and the publish points; they do not prove the deployment's
connection string. That is verified once, by hand, against the deployed app.

**Gotcha:** in the service's default mode the negotiate response redirects the browser to
Azure, so the connection the browser ends up holding is not to our app. Blocking outbound
access from the App Service, or pinning the client to a single transport, breaks this in ways
that look like an authentication failure rather than a networking one.

## What a client may conclude from an event

Stated here because it is the half of the contract that lives in the browser, and a client that
invents its own rules will produce a view that disagrees with the database:

- `isBooked` from the event replaces what the client held, if the sequence is greater.
- `myBookingId` survives only when the event's sequence equals the one the client's own booking
  response returned. Any higher sequence clears it.
- `ScheduleReset` discards the room's snapshot and refetches.
- Reconnect re-joins and refetches.
- Nothing is ever inferred about *who* holds a slot.

## Deliberately out of scope

Guaranteed delivery — an outbox, acknowledgements, or replay from a cursor. Real-time updates
to the room *list* (a room appearing or disappearing while someone watches the listing).
Telling a user directly that an admin cancelled their booking, which is a notification feature
rather than a schedule one. Presence — who else is viewing a room. Serverless or management
modes of the service. Each is an addition to this model rather than a revision of it.
