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

Open http://localhost:4200, sign in with Microsoft, and you land on your tenant's dashboard.
See [Frontend authentication](#frontend-authentication).

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

Tenant-owned tables (those whose entity implements `ITenantOwned`: `Users` and `AuditEvents`) are protected twice:

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
        │            user not provisioned / deactivated ► 403 (all but /me)
        ▼
 Endpoint ── EF query filter + SQL RLS scope the data to demo
```

### Policies

| Policy | Requires | Used by |
|---|---|---|
| Fallback | signed in + `access_as_user` scope | anything without its own policy |
| `ApiScope` | signed in + `scp` contains `access_as_user` | `GET /api/platform/my-tenant` |
| `TenantMember` | `ApiScope` + token `tid` = tenant's `EntraTenantId` | building block |
| `TenantSignIn` | `TenantMember` + role `Admin` or `User` | `GET /api/{tenant}/me` only (it provisions the caller) |
| `TenantUser` | `TenantSignIn` + **active, provisioned user** (`ActiveTenantUser`) | `GET /api/{tenant}/info`, `GET /api/{tenant}/me/activity` |
| `TenantAdmin` | `TenantMember` + role `Admin` + `ActiveTenantUser` | everything in [Dashboards](#dashboards): users, audit, summary, settings |
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

## Frontend authentication

The SPA signs users in with **MSAL** (`@azure/msal-browser` + `@azure/msal-angular`, standalone providers in
[`core/auth/msal.config.ts`](src/web/src/app/core/auth/msal.config.ts)). It uses the Authorization Code flow with PKCE,
by redirect, against `https://login.microsoftonline.com/organizations`. Tokens are cached in `sessionStorage`.

### Flow

```text
/login ── "Sign in with Microsoft" ──► loginRedirect(scope access_as_user) ──► Entra sign-in
                                                                                   │
   http://localhost:4200/#code=... ◄───────────────────────────────────────────────┘
        │
 app startup: initialize() → handleRedirectObservable() → setActiveAccount()   (before the router runs)
        │
 /login: SessionService.bootstrap()
        ├─ GET /api/platform/my-tenant ── 404 ──► /unregistered
        └─ GET /api/{slug}/me ─────────── 403 ──► /forbidden (shows the API's detail)
                │ 200
                ▼
        Admin ──► /{slug}/admin        User ──► /{slug}/user
```

- **MsalInterceptor** attaches `Authorization: Bearer <token>` to `${apiBaseUrl}/api/*`, but not to `/api/health` or
  `/api/health/*`. It gets tokens silently from the cache, or refreshes them.
- **Guards** (in `core/guards`):
  - `authGuard`: you must be signed in.
  - `tenantGuard`: the URL's tenant must be yours. `/other/admin` is rewritten to `/demo/admin`.
  - `adminGuard`: Admin role required, otherwise you go to `/{slug}/user`.
- **HTTP errors:** a 401 from the API sends you to `/login`, and a 403 to `/forbidden`.

Guards only shape the user experience. The **API is the security boundary**: it checks the token, `tid`, scope and role on every request.

### Configuration

| File | Keys |
|---|---|
| `src/web/src/environments/environment.development.ts` | `apiBaseUrl` (`https://localhost:7294`), `msal.clientId`, `msal.authority`, `msal.apiScope` |
| `src/web/src/environments/environment.ts` (production) | same; `apiBaseUrl` is `<api-base-url>`, set by CI/CD |

The SPA client id and the API scope are public identifiers, so they are committed. There are no secrets: a SPA is a public client.
The SPA app registration needs `http://localhost:4200` (and `/login`) as **SPA** redirect URIs. Its API permission
`access_as_user` must be granted (consented) in each organization.

### Run it

1. Start the API with `--launch-profile https`, as described above, and trust the dev certificate.
2. Run `cd src/web && npm start`, then open http://localhost:4200 and sign in.
3. In DevTools → Network, API calls carry `Authorization: Bearer …`. `/api/health` does not.

### Common MSAL errors

| Error | Meaning | Fix |
|---|---|---|
| `AADSTS50011` | Redirect URI mismatch | Add the exact origin (e.g. `http://localhost:4200`) as a **SPA** platform redirect URI, not "Web" |
| `AADSTS65001` | The user or admin hasn't consented to `access_as_user` | Grant admin consent for the SPA's API permission in that organization, or sign in once as an admin and consent |
| `interaction_in_progress` | A second login or redirect started while one was running | Wait or reload. If it persists, clear the tab's `sessionStorage` (stale `msal.interaction.status` key) |
| 401 right after sign-in | The API rejected the token (wrong audience or scope) | Check `msal.apiScope` matches the API's `api://<clientId>/access_as_user` |

## Dashboards

After sign-in, Admins land on `/{tenant}/admin` and Users on `/{tenant}/user`. The UI is Angular Material
(Material 3 theme; light or dark follows the OS `prefers-color-scheme`). On narrow screens the side navigation
becomes a drawer.

| Page | Route | What it shows | API |
|---|---|---|---|
| Admin › Overview | `/{tenant}/admin` | totals (users, active, admins, sign-ins in the last 7 days) and the 5 latest events | `GET dashboard/summary`, `GET audit?pageSize=5` |
| Admin › Users | `/{tenant}/admin/users` | searchable, filterable, sortable, paged table; deactivate/reactivate from the row menu | `GET users`, `POST users/{id}/deactivate`, `POST users/{id}/reactivate` |
| Admin › Activity | `/{tenant}/admin/activity` | the organization's audit log, newest first | `GET audit` |
| Admin › Settings | `/{tenant}/admin/settings` | rename the organization (the slug and URLs do not change) | `PUT settings` |
| User › Overview | `/{tenant}/user` | my profile and my last 10 sign-ins | `GET me/activity` |

All API paths are under `/api/{tenant}/`. Screenshots:

| Admin overview | Users | User overview |
|---|---|---|
| _screenshot: docs/images/admin-overview.png_ | _screenshot: docs/images/admin-users.png_ | _screenshot: docs/images/user-overview.png_ |

### Roles vs. activation

These are two separate switches, owned by two different parties:

| | **Role** (Admin / User) | **Activation** (active / inactive) |
|---|---|---|
| Owned by | the customer's Entra admin | the customer's Admins, inside this app |
| Stored in | the access token (`roles` claim) | our database (`Users.IsActive`) |
| Changed in | [Microsoft Entra admin center](https://entra.microsoft.com) › Enterprise applications › Users and groups | Admin › Users › row menu |
| Takes effect | at the user's next token (up to ~1 hour) | immediately |

- The app **never edits roles**. The stored `Users.Role` is only a copy, re-synced from the token on every `/me`.
- **Deactivating** someone blocks this app only. Their Microsoft account and their other apps are untouched.
  Every TenantUser/TenantAdmin endpoint checks activation (`ActiveTenantUser`). The lookup is cached for
  30 seconds and evicted on deactivate/reactivate, so the change is immediate on that API instance. With several
  instances, other instances may take up to 30 seconds.
- You can't deactivate yourself (400), so an organization can't lock out its last Admin by accident.

### Audit log

`AuditEvents` is tenant-owned, so it gets the EF query filter **and** RLS. The migration `AddAuditEventsAndRls`
adds its predicates to the existing `Security.TenantIsolationPolicy`; check it on Azure SQL with
[`docs/rls-verification.sql`](docs/rls-verification.sql). Each event is saved in the same `SaveChanges` as the
change it describes.

| Event | Written when |
|---|---|
| `UserProvisioned` | a user's first `/me` |
| `UserSignedIn` | `/me`, at most once per 30 minutes per user (the SPA calls `/me` on every load) |
| `UserDeactivated` / `UserReactivated` | an Admin changes activation (not when it was already in that state) |
| `TenantSettingsUpdated` | an Admin renames the organization |

### Bundle budget

The initial-bundle warning budget is **800 kB** (the hard error stays at 1 MB). Material components are imported
per component, and every page is lazy-loaded. The shell (toolbar, sidenav, icons) is needed on the first signed-in
screen, which adds about 120 kB raw (about 25 kB gzipped) to the initial bundle. MSAL accounts for about 340 kB of
the rest. The side navigation uses plain links rather than `mat-nav-list`, because that module would also pull
`@angular/forms` into the initial bundle.

## Build and test

```bash
dotnet build
dotnet test
cd src/web && npm run build && npm test
```

## CI/CD

Two GitHub Actions workflows, with **no secrets** anywhere: Azure trusts GitHub's OIDC token instead.

```text
 pull_request ──► ci.yml ──► CI / api  (restore, build -warnaserror, test + TRX + coverage)
                         └─► CI / web  (npm ci, vitest single run, production build)

 push to main ──► deploy.yml
                    build ──► artifacts: api, efbundle, web
                      │
                      ▼
                    deploy  [environment: production, waits for reviewer approval]
                      1. azure/login (OIDC)
                      2. add SQL firewall rule gh-<run_id> ─► run efbundle ─► remove rule (always)
                      3. App Service  ◄── api
                      4. Static Web Apps ◄── web (token fetched at runtime)
                      5. smoke test /api/health and /api/health/ready
```

### How OIDC works here

1. The `deploy` job has `permissions: id-token: write`, so it can ask GitHub for a short-lived signed JWT.
   Its `sub` (subject) claim is `repo:<owner>/<repo>:environment:production`.
2. `azure/login` sends that JWT to Entra ID and asks for a token for the deploy identity (`vars.AZURE_CLIENT_ID`).
3. Entra checks the signature (GitHub's public keys), the issuer, the audience (`api://AzureADTokenExchange`)
   and that `sub` matches the identity's **federated credential** exactly. Only then does it issue an Azure token.
4. The Azure CLI uses that token for about an hour. Nothing long-lived is stored in GitHub.

`vars.*` hold identifiers (client, tenant and subscription IDs, resource names). They are not secrets.

### Approval gate

The `production` environment has a required reviewer. The deploy job pauses until someone approves it.
The federated credential only accepts the `environment:production` subject, so no other job, branch or
workflow can sign in to Azure, even if someone edits a workflow in a PR.

### Database migrations

- `build` creates a self-contained linux-x64 **EF Core migrations bundle**. `dotnet-ef` is pinned in
  `.config/dotnet-tools.json`.
- `deploy` opens a firewall rule only for the runner's IP and runs the bundle with
  `Authentication=Active Directory Default`. The connection uses the Azure CLI login, and the deploy
  identity is a database user with ddladmin/datareader/datawriter.
- If the first attempt fails, the bundle is retried once after 60 s, because a paused serverless database
  can take up to a minute to resume.
- The firewall rule is removed in an `if: always()` step.
- A bundle only applies migrations missing from `__EFMigrationsHistory`, so re-running it is safe.
- The app itself never migrates at startup. Its managed identity only needs read and write access.

### Re-running

- **CI:** use "Re-run jobs" on the PR, or run the workflow manually (`workflow_dispatch`).
- **Deploy:** Actions → Deploy → "Run workflow" on `main` (or "Re-run all jobs"). Approval is required again.
  Runs are serialized by `concurrency: deploy-production` and are never cancelled midway.
- **Leftover firewall rule:** if a cancelled run left one, delete `gh-<run_id>` in the SQL server's
  Networking blade.
- Make `CI / api` and `CI / web` required status checks in the branch protection for `main`.

## Configuration

Committed config files contain **placeholders only**, because this repo is public.
Real values live in Key Vault, App Service settings or user secrets, and are never committed.

- `src/api/SaasDemo.Api/appsettings.json`: `AzureAd`, `ConnectionStrings`, `Cors:AllowedOrigins`
- `src/web/src/environments/*.ts`: `apiBaseUrl` and MSAL placeholders
- Application Insights turns on only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set.
