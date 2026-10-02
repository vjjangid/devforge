using DevForge.Domain.Builds;
using DevForge.Domain.Common;
using DevForge.Domain.Deployments;

namespace DevForge.Domain.Applications;

/// <summary>
/// An application registered with DevForge. Named <c>App</c> rather than <c>Application</c>
/// because the latter collides with the <c>DevForge.Application</c> layer namespace.
/// </summary>
public sealed class App
{
    public const int NameMaxLength = 100;
    public const int BranchMaxLength = 255;
    public const int DescriptionMaxLength = 1000;

    private readonly List<Deployment> _deployments = [];
    private readonly List<Build> _builds = [];

    private App()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public RepositoryUrl RepositoryUrl { get; private set; } = null!;
    public string Branch { get; private set; } = null!;
    public string Runtime { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<Deployment> Deployments => _deployments;
    public IReadOnlyCollection<Build> Builds => _builds;

    public static App Create(
        string? name,
        string? repositoryUrl,
        string? branch,
        string? runtime,
        string? description,
        DateTimeOffset now)
    {
        var app = new App
        {
            Id = Guid.CreateVersion7(now),
            Runtime = RuntimeCatalog.Get(runtime).Key,
            CreatedAt = now,
        };
        app.UpdateDetails(name, repositoryUrl, branch, description, now);
        return app;
    }

    public void UpdateDetails(string? name, string? repositoryUrl, string? branch, string? description, DateTimeOffset now)
    {
        Name = Guard.Required(name, nameof(Name), NameMaxLength);
        RepositoryUrl = RepositoryUrl.Create(repositoryUrl);
        Branch = ValidateBranch(branch);
        Description = Guard.Optional(description, nameof(Description), DescriptionMaxLength);
        UpdatedAt = now;
    }

    private static string ValidateBranch(string? branch)
    {
        var value = Guard.Required(branch, nameof(Branch), BranchMaxLength);
        if (value.Any(char.IsWhiteSpace))
        {
            throw new DomainValidationException(nameof(Branch), "Branch must not contain whitespace.");
        }

        return value;
    }
}
