using SaasDemo.Api.Endpoints;
using SaasDemo.Api.Tenancy;
using SaasDemo.Application.Tenancy;
using SaasDemo.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<TenantResolver>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

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
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("spa");
// TODO(step 7.4): add app.UseAuthentication() here, before tenant resolution, so the middleware
// can verify validated JWT tid == ITenantContext.EntraTenantId (reject missing/mismatched with 403).
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();

// Anonymous tenant diagnostics are Development-only; elsewhere the route does not exist.
if (app.Environment.IsDevelopment())
{
    app.MapTenantInfoEndpoint();
}

app.Run();

// Exposes Program to WebApplicationFactory in integration tests.
public partial class Program;
