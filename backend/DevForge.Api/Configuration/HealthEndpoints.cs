namespace DevForge.Api.Configuration;

public static class HealthEndpoints
{
    public const string Liveness = "/health";
    public const string Readiness = "/health/ready";
    public const string ReadyTag = "ready";
    public const string DatabaseCheckName = "postgres";
}
