namespace DevForge.Application.Applications;

/// <summary>Derived from an application's most recent deployment; never stored.</summary>
public enum ApplicationStatus
{
    NeverDeployed,
    Deploying,
    Running,
    Failed,
}
