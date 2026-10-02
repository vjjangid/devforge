namespace DevForge.Application.Platform;

/// <summary>Static facts about this DevForge installation that the UI needs to render forms.</summary>
public sealed record PlatformInfoDto(IReadOnlyList<RuntimeDto> Runtimes, PlatformFeaturesDto Features);

public sealed record RuntimeDto(string Key, string DisplayName);

public sealed record PlatformFeaturesDto(bool FailureSimulation);
