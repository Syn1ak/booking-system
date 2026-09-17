# Meeting Room Booking System

Users view meeting rooms and their bookable time slots and reserve free ones; admins
additionally manage rooms and see every user's bookings. When a booking changes, everyone
viewing that room's schedule sees it immediately.

Backend is ASP.NET Core on .NET 10 with Azure SQL; the frontend is Angular with Bootstrap and
ng-bootstrap, served by the API from the same origin. The system guarantees that concurrent
requests for the same slot result in exactly one booking, with the rest receiving a conflict
response.

Design decisions are recorded in [CLAUDE.md](CLAUDE.md) and the files it links to.

## Prerequisites

- .NET 10 SDK
- Docker (for the local database)
- Node.js 22.22+ or 24.15+ (for the frontend)

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

### Frontend

With the API running, in a second terminal:

```bash
cd src/BookingSystem.Web
npm ci
npm start
```

Open `http://localhost:4200`. The dev server proxies `/api` and `/hub` to the API on
`http://localhost:5103`, so the browser sees a single origin exactly as in production, and the
hub runs over WebSockets through the proxy.

Signing in keeps the token in memory only, never in browser storage, so **refreshing the page
signs you out** and returns you to where you were after signing in again. That is a deliberate
trade-off recorded in [.claude/auth/auth.md](.claude/auth/auth.md). In development builds the
sign-in page offers the demo accounts below as shortcuts.

To see live updates, open the same room's schedule in two browser tabs, sign in as a different
user in each - every tab holds its own session - and book a slot in one.

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

### Real-time updates

Schedule changes are pushed over a SignalR hub at `/hub/schedule`. Locally and under test the
hub runs in-process, so nothing extra is needed to develop against it. Setting
`Azure__SignalR__ConnectionString` switches the same hub onto Azure SignalR Service, which is
what the deployed app does; the hub, its groups and its messages are identical either way.

The hub requires an authenticated connection, and the client passes its token as an
`access_token` query parameter because a browser cannot set headers on a WebSocket handshake.
That is accepted on hub paths only.

## Tests

```bash
dotnet test
```

Frontend unit tests, which include the client's half of the real-time contract:

```bash
cd src/BookingSystem.Web
npm test -- --watch=false
```

Integration tests boot the real application against a throwaway SQL Server container
started by Testcontainers, apply migrations to it, and discard it afterwards. Docker must
be running; nothing else needs setting up, and the Compose database above is untouched.

## Publishing

```bash
dotnet publish src/BookingSystem.Api -c Release -o publish
```

builds the frontend into the API's `wwwroot` and publishes both as one artefact. `dotnet build`
and `dotnet test` never run the frontend build, so they do not need Node.

To stop the database, `docker compose down` — add `-v` to discard the data volume as well.
