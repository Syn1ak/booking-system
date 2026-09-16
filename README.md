# Meeting Room Booking System

Users view meeting rooms and their bookable time slots and reserve free ones; admins
additionally manage rooms and see every user's bookings. When a booking changes, everyone
viewing that room's schedule sees it immediately.

Backend is ASP.NET Core on .NET 10 with Azure SQL. The system guarantees that concurrent
requests for the same slot result in exactly one booking, with the rest receiving a conflict
response.

Design decisions are recorded in [CLAUDE.md](CLAUDE.md) and the files it links to.

## Prerequisites

- .NET 10 SDK
- Docker (for the local database)

## Running locally

Start the database:

```bash
docker compose up -d
```

This runs SQL Server 2022 Developer Edition on `localhost:1433` with the `sa` password
`Local_Dev_Password_1`, and keeps its data in a named volume across restarts. Override the
password by exporting `MSSQL_SA_PASSWORD` before starting. The password is local-only and is
not used in any deployed environment.

On Apple Silicon this works without extra setup: the image is amd64-only, but Docker Desktop
runs it under Rosetta.

Restore the local tools and apply migrations:

```bash
dotnet tool restore
dotnet ef database update --project src/BookingSystem.Api
```

Then run the API:

```bash
dotnet run --project src/BookingSystem.Api
```

The development connection string in `appsettings.Development.json` points at the Compose
database above and carries its local-only password. If you override `MSSQL_SA_PASSWORD`,
override the connection string too via the `ConnectionStrings__Default` environment variable.

### Demo accounts

A fresh database is seeded with two accounts, both development-only:

| Email | Password | Role |
|---|---|---|
| `admin@example.com` | `Admin123!` | Admin |
| `user@example.com` | `User123!` | User |

They come from the `Seed:Users` section of `appsettings.Development.json`. An environment
that configures no seed users - production, unless it opts in - creates none. Existing
accounts are never modified, so re-running the app cannot reset a changed password.

### Demo room

`Seed:Rooms` in the same file creates one room - Board room, 09:00-17:00 UTC, one-hour slots -
so a fresh database has something to look at. A room of that name is never modified or
duplicated on later starts. No slots are seeded: they are created when a date's schedule is
first read.

## Tests

```bash
dotnet test
```

Integration tests boot the real application against a throwaway SQL Server container
started by Testcontainers, apply migrations to it, and discard it afterwards. Docker must
be running; nothing else needs setting up, and the Compose database above is untouched.

To stop the database, `docker compose down` — add `-v` to discard the data volume as well.
