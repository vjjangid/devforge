using System.ComponentModel.DataAnnotations;
using DevForge.Domain.Applications;

namespace DevForge.Application.Applications;

public sealed class CreateApplicationRequest
{
    [Required, StringLength(App.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(Domain.Applications.RepositoryUrl.MaxLength)]
    public string RepositoryUrl { get; init; } = string.Empty;

    [Required, StringLength(App.BranchMaxLength)]
    public string Branch { get; init; } = string.Empty;

    [Required, StringLength(RuntimeCatalog.KeyMaxLength)]
    public string Runtime { get; init; } = string.Empty;

    [StringLength(App.DescriptionMaxLength)]
    public string? Description { get; init; }
}

/// <summary>Runtime is intentionally absent: it is fixed when the application is created.</summary>
public sealed class UpdateApplicationRequest
{
    [Required, StringLength(App.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(Domain.Applications.RepositoryUrl.MaxLength)]
    public string RepositoryUrl { get; init; } = string.Empty;

    [Required, StringLength(App.BranchMaxLength)]
    public string Branch { get; init; } = string.Empty;

    [StringLength(App.DescriptionMaxLength)]
    public string? Description { get; init; }
}
