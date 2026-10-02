using System.Text.Json.Serialization;
using DevForge.Api.Configuration;
using DevForge.Api.ErrorHandling;
using DevForge.Application;
using DevForge.Application.Common;
using DevForge.Infrastructure;
using DevForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<FeatureOptions>(builder.Configuration.GetSection(FeatureOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services
    .AddControllers(options =>
    {
        // Report validation errors under the JSON property names clients actually sent.
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(HealthEndpoints.DatabaseCheckName, tags: [HealthEndpoints.ReadyTag]);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
{
    await app.Services.MigrateDatabaseAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

// Liveness: the process is up and serving requests. No dependencies are checked.
app.MapHealthChecks(HealthEndpoints.Liveness, new HealthCheckOptions { Predicate = _ => false });

// Readiness: the API can reach PostgreSQL and is able to do useful work.
app.MapHealthChecks(HealthEndpoints.Readiness, new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthEndpoints.ReadyTag),
});

await app.RunAsync();

/// <summary>Exposed so integration tests can host the API with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
