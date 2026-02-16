# Heroku Deployment Hardening End Report

## Scope
This follow-up hardening pass addressed deployment-layer concerns from the previous Heroku compatibility implementation while preserving Jellyfin core architecture, plugin loading behavior, and module boundaries.

## Changes Completed

### 1. Database provider runtime override safety
- Kept runtime PostgreSQL auto-selection from `DATABASE_URL`.
- Removed persistence side effect that previously rewrote saved DB config when `DATABASE_URL` was present.
- Result: Heroku-style runtime override no longer permanently mutates configuration state.

### 2. PostgreSQL TLS handling hardening
- Updated `DATABASE_URL` query parsing:
  - `sslmode` now sets only `SslMode`.
  - `TrustServerCertificate` is only enabled when explicitly provided via `trustservercertificate=true`.
- Result: safer defaults for TLS trust behavior.

### 3. Docker runtime env correctness
- Replaced shell-style `ASPNETCORE_URLS` variable interpolation in Docker `ENV` with static fallback (`http://0.0.0.0:8096`).
- Kept Heroku binding logic in server code for dynamic `$PORT` handling.
- Result: avoids misleading/non-portable env expansion assumptions.

### 4. Optional background service control
- Replaced implicit HEROKU-only service suppression with explicit toggle:
  - `JELLYFIN_DISABLE_OPTIONAL_BACKGROUND_SERVICES=true`
- Result: operators can run on Heroku without forcibly disabling these services unless desired.

### 5. Migration fallback scoping
- Scoped `EnsureCreatedAsync()` fallback to provider names containing `Npgsql` when no migration assembly entries exist.
- Result: lower risk of unintended schema behavior on other providers.

### 6. Deployment docs/config alignment
- Updated `Dockerfile`, `app.json`, and README to reflect new explicit memory-control toggle and deployment commands.

## Validation Performed
- JSON validation for `app.json` succeeded.
- Static validation checks of edited source files succeeded.

## Environment Limitations
- `dotnet` CLI is not available in this execution environment.
- Docker CLI is not available in this execution environment.
- Therefore `dotnet publish` and container build could not be executed here.

## Recommended Next Validation in CI/Runtime
1. `dotnet publish Jellyfin.Server/Jellyfin.Server.csproj -c Release`
2. Build and run container with `PORT` override and verify Kestrel bind.
3. Integration smoke test against Heroku Postgres (`DATABASE_URL`) with startup migration path.
4. Memory profile run with and without `JELLYFIN_DISABLE_OPTIONAL_BACKGROUND_SERVICES=true`.
