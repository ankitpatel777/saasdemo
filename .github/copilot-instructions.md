# Project: SaaS Demo (multi-tenant)

## Stack
- Backend: ASP.NET Core 9 Web API, C# 13, EF Core 9, Clean Architecture (Domain, Application, Infrastructure, Api).
- Frontend: Angular 18+ standalone components, signals, MSAL Angular, strict TypeScript.
- Azure: Entra ID (multi-tenant, v2 tokens), Azure SQL (Entra-only auth, Managed Identity), Key Vault, App Service, Static Web Apps, Application Insights.

## Multi-tenancy rules (critical — flag violations in reviews)
- The tenant comes from the URL path `/{tenantSlug}/...` and MUST be verified against the JWT `tid` claim.
- Every tenant-owned entity has `TenantId`. Never query tenant data without the EF global query filter.
- Never trust tenantId from the request body or query string.
- SQL Row-Level Security uses SESSION_CONTEXT('TenantId'); do not bypass it.

## Security rules
- No secrets, connection strings with passwords, or keys in code or config. Use Key Vault and Managed Identity.
- Protect endpoints with [Authorize] and role policies ("Admin", "User"). Anonymous access only where explicit.
- Validate all input (FluentValidation). Return ProblemDetails for errors.

## Code style
- Async all the way; pass CancellationToken.
- Use primary constructors and records for DTOs.
- Use structured logging (ILogger), no Console.WriteLine.
- Unit tests: xUnit + FluentAssertions; Angular: Jest or Karma per project setup.

## Review focus
- Tenant isolation, authorization gaps, N+1 queries, missing cancellation tokens, unhandled errors, secrets in code.