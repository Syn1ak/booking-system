# CLAUDE.md

## What this is

A meeting room booking system. Users view rooms and their bookable time slots and reserve
free ones; admins additionally manage rooms and see every user's bookings. Backend is
ASP.NET Core on .NET 10 with Azure SQL; the frontend is a single-page app served from the
same origin; booking changes are pushed to connected viewers over Azure SignalR.

The graded centrepiece is **concurrency**: when several requests target the same slot at the
same moment, exactly one must succeed and the rest must receive a clear conflict response —
never a silent overwrite, never a server error. Design decisions are weighted accordingly.

## Where the decisions are recorded

Read the relevant file before changing code in that area. Each records what was chosen, why,
and what was rejected — so decisions are not re-derived or quietly reversed.

| Area | File |
|---|---|
| Code organisation, layering, data access | [.claude/architecture/architecture.md](.claude/architecture/architecture.md) |
| Authentication, roles, authorization | [.claude/auth/auth.md](.claude/auth/auth.md) |
| Slot model | [.claude/scheduling/scheduling.md](.claude/scheduling/scheduling.md) |
| Concurrency control | [.claude/concurrency/concurrency.md](.claude/concurrency/concurrency.md) |
| Real-time updates | *not yet decided* |

If a change contradicts one of these, update the file in the same commit. A decision file
that disagrees with the code is worse than no file.

## Conventions

**Commits are atomic.** One coherent change per commit, and each leaves the repository
building. The message gives a short subject, then *what changed and why* — the why matters
more, since the what is visible in the diff.

**Rationale lives in commit messages and decision files, not in comments.** A comment earns
its place only when it says something the code cannot: an opaque constant, a non-obvious
constraint, a gotcha that already cost time. Do not comment self-describing code.

**Decide before implementing.** Where a design choice is open, record it first. This is
graded on judgement, not only on working code.

## Commands

```
docker compose up -d  # local SQL Server on 1433; first run pulls the image
docker compose down   # stop it; add -v to discard the data volume
dotnet tool restore   # once per clone; pins dotnet-ef
dotnet ef database update --project src/BookingSystem.Api
dotnet build          # from the repository root
dotnet run --project src/BookingSystem.Api
dotnet test           # integration tests; needs Docker, starts its own SQL Server
```

Integration tests run against a throwaway SQL Server container via Testcontainers, not
against the Compose database - so they never read or write local development data.
