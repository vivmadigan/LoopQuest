using LoopQuest.Api.Common;
using LoopQuest.Application;
using LoopQuest.Application.Common.Interfaces;
using LoopQuest.Infrastructure;
using LoopQuest.Infrastructure.Persistence;
using LoopQuest.Infrastructure.Strava;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, service discovery, HTTP resilience.
builder.AddServiceDefaults();

// Application layers (composition roots).
builder.AddInfrastructure();        // Aspire PostgreSQL + EF Core (extends the host builder)
builder.Services.AddApplication();  // MediatR + validators + pipeline behaviours

// Web API services.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();

var app = builder.Build();

// Convert unhandled exceptions (incl. ValidationException) into problem-details responses.
app.UseExceptionHandler();

// Aspire liveness/readiness endpoints: /health and /alive.
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                // OpenAPI document at /openapi/v1.json
    app.MapScalarApiReference();     // Interactive API explorer at /scalar/v1
}

// No UseHttpsRedirection: TLS is terminated at the edge (Aspire locally, the ingress in Azure),
// so the service itself speaks plain HTTP in-cluster. Re-add it if you host the API directly.
app.MapControllers();

// Apply pending migrations and seed the loop library on startup.
// See DatabaseInitializer for the note on doing this differently in production.
await app.Services.InitializeDatabaseAsync();

app.Run();
