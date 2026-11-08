# SaaS Demo

A learning project: a **multi-tenant SaaS** on Azure, built with an ASP.NET Core 9 API
(Clean Architecture) and an Angular SPA. Each customer organisation (tenant) signs in with
its own Microsoft Entra ID and reaches the app at `/{tenant}/...`.

> Status: skeleton only. The API exposes a health endpoint and the SPA calls it.
> Authentication, the database and tenant features come in later PRs.

## Architecture

```
            Browser
               |
               v
  +---------------------------+        +-------------------------------+
  | Azure Static Web Apps     |  HTTPS | Azure App Service (Linux)     |
  | Angular SPA (src/web)     | -----> | ASP.NET Core 9 API (src/api)  |
  |  /login                   |  CORS  |                               |
  |  /:tenant/admin           |        |  Api            (HTTP, DI)    |
  |  /:tenant/user            |        |   |-> Application (use cases) |
  +---------------------------+        |   |    `-> Domain (entities) |
               |                       |   `-> Infrastructure (I/O)   |
               | sign-in (later)       +-------------------------------+
               v                           |          |           |
  +---------------------------+            v          v           v
  | Microsoft Entra ID        |      Azure SQL    Key Vault   Application
  | (multi-tenant, v2 tokens) |     (Entra-only,  (secrets)    Insights
  +---------------------------+   Managed Identity)
```

Dependencies point inward: `Api -> Application, Infrastructure`,
`Infrastructure -> Application`, `Application -> Domain`. Domain depends on nothing.

## Repository layout

```
SaasDemo.sln                 .NET solution (repo root)
Directory.Build.props        shared .NET settings (net9.0, nullable, warnings as errors)
src/api/SaasDemo.Domain      entities and business rules
src/api/SaasDemo.Application use cases and interfaces
src/api/SaasDemo.Infrastructure  implementations of those interfaces (DB, Azure SDKs)
src/api/SaasDemo.Api         HTTP host: controllers, middleware, configuration
tests/SaasDemo.UnitTests     xUnit + FluentAssertions
src/web                      Angular SPA
```

## Prerequisites

- [.NET SDK 9](https://dotnet.microsoft.com/download) (the version is pinned in `global.json`)
- [Node.js](https://nodejs.org/) LTS and npm
- A trusted HTTPS dev certificate: `dotnet dev-certs https --trust`

## Running locally

**API** (in one terminal, from the repo root):

```bash
dotnet run --project src/api/SaasDemo.Api --launch-profile https
```

- Health: https://localhost:7294/api/health
- API reference (Scalar, Development only): https://localhost:7294/scalar/v1

**Web** (in a second terminal):

```bash
cd src/web
npm install
npm start          # ng serve on http://localhost:4200
```

Open http://localhost:4200/login. You should see **API status: ok**.

## Database

The API uses EF Core 9 with Azure SQL (`sqldb-saasdemo-dev`), with **Entra-only, passwordless** authentication.
Locally you connect as yourself, and in Azure the App Service managed identity connects. No password exists anywhere.

### First-time setup

1. Sign in so `Active Directory Default` can find your identity: `az login` (or sign in to Visual Studio).
2. Make sure your public IP is allowed in the Azure SQL server firewall.
3. Store the connection string in user secrets. They live outside the repo, so never put it in `appsettings*.json`:

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
     "Server=tcp:<server>.database.windows.net,1433;Database=sqldb-saasdemo-dev;Authentication=Active Directory Default;Encrypt=True;Connect Timeout=60;" \
     --project src/api/SaasDemo.Api

   # Optional: the Entra tenant id given to the seeded "demo" tenant (defaults to all zeros)
   dotnet user-secrets set "Dev:DemoEntraTenantId" "<entra-tenant-id>" --project src/api/SaasDemo.Api
   ```

   **Why `Connect Timeout=60`:** `Active Directory Default` tries several credential sources in turn: environment,
   managed identity, Visual Studio, then Azure CLI. On some machines the Visual Studio source hangs. With the default
   15-second timeout, the connection then fails with *"A task was canceled"* (in `VisualStudioCredential`) before it
   reaches your `az login`. A 60-second budget lets it fall through.

4. Apply the migrations (`InitialCreate`, then `AddRowLevelSecurity`):

   ```bash
   dotnet tool install --global dotnet-ef   # once
   dotnet ef database update --project src/api/SaasDemo.Infrastructure --startup-project src/api/SaasDemo.Api
   ```

5. Run the API. In Development, a seeder creates the `demo` tenant if it is missing. Then check:
   - https://localhost:7294/api/health/ready should return `Healthy`. This checks the database, while `/api/health` is liveness only.
   - `/api/demo/info`, `/api/demo/me` and `/api/demo/users` need an access token: see [Authentication](#authentication).

New migration: `dotnet ef migrations add <Name> --project src/api/SaasDemo.Infrastructure --startup-project src/api/SaasDemo.Api --output-dir Persistence/Migrations`.

In Azure, the connection string comes from the App Service setting `ConnectionStrings__DefaultConnection` (a Key Vault reference).

### How tenant isolation works

Tenant-owned tables (those whose entity implements `ITenantOwned`, today only `Users`) are protected twice:

1. **EF Core (app layer).** A global query filter adds `WHERE TenantId = <current tenant>` to every query.
   `SaveChanges` stamps the current tenant on new rows. It throws if a row belongs to another tenant or no tenant is resolved.
2. **SQL Row-Level Security (database layer).** On every connection open, `TenantSessionContextInterceptor`
   runs `sp_set_session_context 'TenantId'` (read-only). The `Security.TenantIsolationPolicy` uses this value:
   - Its filter predicate hides other tenants' rows.
   - Its block predicates reject inserts and updates into another tenant.

   This still holds if application code bypasses EF filters (e.g. `IgnoreQueryFilters()` or raw SQL).
   - `db_owner` bypasses the policy so the admin can inspect data. **The app identity must never be `db_owner`.**
   - Pooled connections are safe: `sp_reset_connection` clears the session context when a connection is reused.

The automated tests run on SQLite, which has no RLS. To verify RLS for real on Azure SQL, run
[`docs/rls-verification.sql`](docs/rls-verification.sql).

### Serverless: the first call can be slow

The dev database is serverless and auto-pauses when idle. The first request after a pause wakes it up, which can take up to about a minute.
EF retries transient failures for up to about 60 seconds, with a 60-second command timeout. So the first call (or `/api/health/ready`) may be slow
rather than fail, and later calls are fast.

## Authentication

The API accepts **Entra ID v2 access tokens** (Microsoft.Identity.Web). The app registration is multi-tenant, so a user
from *any* Entra organization can get a valid token. Signing in proves who you are, not that you belong to a client.
Every tenant route therefore also checks that the token's directory (`tid`) owns the tenant in the URL.

### Request flow

```text
GET /api/demo/users   Authorization: Bearer eyJ...
        │
        ▼
 UseCors ─► UseHttpsRedirection
        │
        ▼
 UseAuthentication ── no/invalid token ─────────────► (anonymous; continues)
        │  valid: signature, issuer (any Entra tenant, must match tid), audience, expiry
        ▼
 TenantResolution ── anonymous ────────────────────► skipped (no lookups for anonymous callers)
        │            bad slug ──► 400 · unknown/inactive ──► 404
        │  ITenantContext = demo (EntraTenantId = <tid of the demo directory>)
        ▼
 UseAuthorization ── anonymous ────────────────────► 401 ProblemDetails
        │            no access_as_user scope ──────► 403
        │            token tid ≠ demo's EntraTenantId ► 403 (logged as a warning)
        │            role missing for this endpoint ──► 403
        ▼
 Endpoint ── EF query filter + SQL RLS scope the data to demo
```

### Policies

| Policy | Requires | Used by |
|---|---|---|
| Fallback | signed in + `access_as_user` scope | anything without its own policy |
| `ApiScope` | signed in + `scp` contains `access_as_user` | `GET /api/platform/my-tenant` |
| `TenantMember` | `ApiScope` + token `tid` = tenant's `EntraTenantId` | building block |
| `TenantUser` | `TenantMember` + role `Admin` or `User` | `GET /api/{tenant}/info`, `GET /api/{tenant}/me` |
| `TenantAdmin` | `TenantMember` + role `Admin` | `GET /api/{tenant}/users` |
| anonymous | nothing | `/api/health`, `/api/health/ready`, OpenAPI/Scalar (Development) |

401 and 403 responses are ProblemDetails. A 403's `detail` says what is missing, e.g. *"No role assigned in your organization."*

`GET /api/{tenant}/me` provisions users **just in time**:
- The first call creates the user, keyed by tenant and `oid`.
- Every call re-syncs the role from the token's `roles` claim (Admin wins) and updates `LastLoginAtUtc`.

### Configuration

| Key | Value |
|---|---|
| `AzureAd:Instance` | `https://login.microsoftonline.com/` |
| `AzureAd:TenantId` | `organizations` (multi-tenant) |
| `AzureAd:ClientId` | API app registration client id (user secrets / App Service setting) |
| `AzureAd:Audience` | `api://<clientId>` (both `<clientId>` and `api://<clientId>` are accepted) |
| `Dev:DemoEntraTenantId` | Development only: your directory id, so the seeded `demo` tenant accepts your tokens |

### Getting a test token

You need these one-time setup steps:
- **Authorize the Azure CLI:** on the API app registration, under *Expose an API*, add the Azure CLI
  (`04b07795-8ddb-461a-bbee-02f9e1bf7b46`) as an authorized client application for `access_as_user`.
- **Assign yourself a role:** in *Enterprise applications* → the API → *Users and groups*, assign yourself the `Admin` or `User` app role.

```bash
az login
az account get-access-token --scope api://<clientId>/access_as_user --query accessToken -o tsv
```

Then paste the token into Scalar (https://localhost:7294/scalar/v1, *Authentication → Bearer*) or send it with curl:

```bash
curl -H "Authorization: Bearer <token>" https://localhost:7294/api/platform/my-tenant   # → {"slug":"demo",...}
curl -H "Authorization: Bearer <token>" https://localhost:7294/api/demo/me
```

Tokens are credentials: don't paste them into issues, chats or logs. They expire after about an hour.

## Build and test

```bash
dotnet build
dotnet test
cd src/web && npm run build && npm test
```

## Configuration

Committed config files contain **placeholders only**, because this repo is public.
Real values live in Key Vault, App Service settings or user secrets, and are never committed.

- `src/api/SaasDemo.Api/appsettings.json`: `AzureAd`, `ConnectionStrings`, `Cors:AllowedOrigins`
- `src/web/src/environments/*.ts`: `apiBaseUrl` and MSAL placeholders
- Application Insights turns on only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set.
