namespace DevForge.IntegrationTests.Execution;

/// <summary>
/// A test that drives a real Docker daemon. Skipped unless <c>DEVFORGE_DOCKER_TESTS=1</c>, so the
/// default test run needs nothing beyond PostgreSQL and git.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute
{
    private const string Variable = "DEVFORGE_DOCKER_TESTS";

    public DockerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) != "1")
        {
            Skip = $"Needs a Docker daemon. Set {Variable}=1 to run.";
        }
    }
}
