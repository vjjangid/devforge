namespace DevForge.Api.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF Core migrations when the API starts.</summary>
    public bool MigrateOnStartup { get; init; }
}
