# Knot Shore Control Plane

The control-plane service for the Knot Shore Grocery Platform. It stores
scenario *definitions* — the metadata describing a data-generation run (scope,
seasonality, growth, realism noise, anomaly weighting, and a determinism seed).
Generated sales data is data-plane volume and is never stored here; this service
holds intent only.

Built on ASP.NET Core (.NET 8) with EF Core and PostgreSQL. ASP.NET Identity backs
user registration and login, and the service issues JWT bearer tokens. Scenario
endpoints are not yet implemented.

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

JWT bearer authentication is configured under the `Jwt` section. The issuer and
audience are non-secret defaults in `appsettings.json`; the **signing key is a
secret** and is supplied at run time via an environment variable. `appsettings.json`
carries a placeholder only.

```
Jwt__SigningKey=<a long random secret, at least 32 bytes>
Jwt__Issuer=knot-shore-control-plane
Jwt__Audience=knot-shore-control-plane
```

`Jwt__AccessTokenMinutes` (default 60) controls token lifetime.

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

## Authentication

The service uses ASP.NET Identity for user accounts and issues JWT bearer tokens.

- `POST /auth/register` — body `{ "email", "password", "userName?" }`. Creates a
  user. Returns the new user's id and email; password-policy violations come back
  as validation errors.
- `POST /auth/login` — body `{ "email", "password" }`. On success returns
  `{ "accessToken", "expiresAtUtc", "tokenType" }`. On failure returns `401`
  without indicating whether the email or the password was wrong.
- `GET /auth/me` — requires a valid bearer token; returns the caller's id and email
  from the token claims. Without a valid token it returns `401`.

Send the token as `Authorization: Bearer <accessToken>` on requests to protected
endpoints. `GET /health` is unauthenticated.

## Tests

```
dotnet test
```

Persistence and endpoint tests run against EF Core's in-memory SQLite provider and
require no external database. Endpoint tests host the app with
`WebApplicationFactory` and supply a test-only JWT signing key in memory.
