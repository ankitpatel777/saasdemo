# SaaS Demo

A learning project: a **multi-tenant SaaS** on Azure, built with an ASP.NET Core 9 API
(Clean Architecture) and an Angular SPA. Each customer organisation (tenant) signs in with
its own Microsoft Entra ID and reaches the app at `/{tenant}/...`.

> Status: the API exposes an anonymous health endpoint and tenant API routes require
> an Entra bearer token whose tenant ID matches the configured tenant for the URL slug.
> The database and tenant-owned features come in later PRs.

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

## Build and test

```bash
dotnet build
dotnet test
cd src/web && npm run build && npm test
```

## Configuration

Committed config files contain **placeholders only**, because this repo is public.
Real values live in Key Vault, App Service settings or user secrets, and are never committed.

- `src/api/SaasDemo.Api/appsettings.json`: `AzureAd`, `ConnectionStrings`, `Cors:AllowedOrigins`,
  and `Tenants` (map each tenant URL slug to its Entra tenant ID GUID)
- `src/web/src/environments/*.ts`: `apiBaseUrl` and MSAL placeholders
- Application Insights turns on only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set.
