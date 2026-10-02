using DevForge.Application.Abstractions;
using DevForge.Application.Common;
using DevForge.Domain.Applications;
using DevForge.Domain.Builds;
using DevForge.Domain.Deployments;
using DevForge.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevForge.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    private const string GenericConflictMessage = "The change conflicts with an existing record.";

    private static readonly Dictionary<string, string> ConflictMessages = new(StringComparer.Ordinal)
    {
        [AppConfiguration.UniqueNameIndex] = "An application with this name already exists.",
        [DeploymentConfiguration.OneActivePerApplicationIndex] = "This application already has a deployment in progress.",
    };

    public DbSet<App> Applications => Set<App>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<Build> Builds => Set<Build>();
    public DbSet<DeploymentLog> DeploymentLogs => Set<DeploymentLog>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            // Unique indexes are the last line of defence against concurrent requests;
            // surface them as a domain-level conflict rather than a 500.
            var message = postgres.ConstraintName is { } constraint && ConflictMessages.TryGetValue(constraint, out var known)
                ? known
                : GenericConflictMessage;

            throw new ConflictException(message, exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
