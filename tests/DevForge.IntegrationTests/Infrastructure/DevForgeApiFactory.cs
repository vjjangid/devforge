using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DevForge.IntegrationTests.Infrastructure;

/// <summary>Hosts the real API in memory against a test database, migrating it on startup.</summary>
internal sealed class DevForgeApiFactory(string connectionString, bool failureSimulation = true) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting (rather than ConfigureAppConfiguration) so the values are visible while Program.cs runs.
        builder.UseSetting("ConnectionStrings:DevForge", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Features:FailureSimulation", failureSimulation.ToString());
    }
}
