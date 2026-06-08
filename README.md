# Knot Shore Control Plane

The control-plane service for the Knot Shore Grocery Platform. It stores
scenario *definitions* — the metadata describing a data-generation run (scope,
seasonality, growth, realism noise, anomaly weighting, and a determinism seed).
Generated sales data is data-plane volume and is never stored here; this service
holds intent only.

Built on ASP.NET Core (.NET 8) with EF Core and PostgreSQL. ASP.NET Identity is
included in the schema; authentication flows and scenario endpoints are not yet
implemented.

## Prerequisites

- .NET 8 SDK
- PostgreSQL (a reachable instance for running migrations and the service)

## Configuration

The PostgreSQL connection string is read from configuration under
`ConnectionStrings:ControlPlane`. `appsettings.json` carries a non-secret
placeholder; supply the real value at run time via the environment variable:

```
ConnectionStrings__ControlPlane=Host=...;Port=5432;Database=...;Username=...;Password=...
```

No credentials are committed. Use environment variables or
[user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) for
local development.

## Database migrations

The EF Core CLI is pinned as a local tool. Restore it once, then apply
migrations against the database named in the connection string:

```
dotnet tool restore
dotnet ef database update --project src/KnotShoreControlPlane
```

This creates the ASP.NET Identity tables and the `Scenarios` table.

## Running the service

```
dotnet run --project src/KnotShoreControlPlane
```

The service exposes `GET /health`, which returns `200 OK` with `{"status":"ok"}`.

## Tests

```
dotnet test
```

Persistence tests run against EF Core's in-memory SQLite provider and require no
external database.
