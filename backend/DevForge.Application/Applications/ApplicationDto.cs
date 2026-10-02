using DevForge.Domain.Deployments;

namespace DevForge.Application.Applications;

public sealed record ApplicationDto(
    Guid Id,
    string Name,
    string RepositoryUrl,
    string RepositoryDisplayName,
    string Branch,
    string Runtime,
    string RuntimeDisplayName,
    string? Description,
    ApplicationStatus Status,
    DeploymentSummaryDto? LastDeployment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DeploymentSummaryDto(
    Guid Id,
    int Number,
    string Version,
    DeploymentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
