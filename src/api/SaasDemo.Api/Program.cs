using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SaasDemo.Api.Authorization;
using SaasDemo.Api.Endpoints;
using SaasDemo.Api.OpenApi;
using SaasDemo.Api.Tenancy;
using SaasDemo.Application.Tenancy;
using SaasDemo.Infrastructure;
using SaasDemo.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<TenantResolver>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

builder.Services.AddScoped<IValidator<UsersQueryParameters>, UsersQueryParametersValidator>();
builder.Services.AddScoped<IValidator<AuditQueryParameters>, AuditQueryParametersValidator>();
builder.Services.AddScoped<IValidator<UpdateSettingsRequest>, UpdateSettingsRequestValidator>();

builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddApiAuthorization();

if (builder.Environment.IsDevelopment())
{
    // Ensures the "demo" tenant exists and is linked to Dev:DemoEntraTenantId. Never registered outside Development.
    builder.Services.AddHostedService<DevelopmentDataSeeder>();
}

// Enable telemetry only when APPLICATIONINSIGHTS_CONNECTION_STRING is configured (e.g. in App Service).
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];
builder.Services.AddCors(options => options.AddPolicy("spa", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 401/403/404 responses become ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// Order matters (see README "Authentication"):
app.UseCors("spa");                                // 1. CORS first, so preflights and error responses carry CORS headers
app.UseHttpsRedirection();                         // 2. never process tokens over plain HTTP
app.UseAuthentication();                           // 3. who are you? validates the bearer token, sets HttpContext.User
app.UseMiddleware<TenantResolutionMiddleware>();   // 4. which tenant is this URL? (needs User, see middleware)
app.UseAuthorization();                            // 5. may you do this here? (TenantMember needs both 3 and 4)

app.MapControllers();

// /api/health is liveness (no dependencies, HealthController); /api/health/ready also checks the database.
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(DependencyInjection.ReadyTag),
}).AllowAnonymous();

app.MapPlatformEndpoints();
app.MapTenantEndpoints();

app.Run();

// Exposes Program to WebApplicationFactory in integration tests.
public partial class Program;
