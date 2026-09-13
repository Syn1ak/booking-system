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

To stop the database, `docker compose down` — add `-v` to discard the data volume as well.
