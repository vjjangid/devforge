using DevForge.Domain.Common;

namespace DevForge.Domain.Applications;

/// <summary>
/// The runtimes DevForge knows how to build. Applications store the stable <see cref="RuntimeDefinition.Key"/>,
/// so adding a runtime is a single new entry here.
/// </summary>
public static class RuntimeCatalog
{
    public const int KeyMaxLength = 50;

    public static readonly RuntimeDefinition DotNet10 = new("dotnet-10", ".NET 10");

    public static IReadOnlyList<RuntimeDefinition> All { get; } = [DotNet10];

    public static RuntimeDefinition? Find(string? key) =>
        All.FirstOrDefault(runtime => string.Equals(runtime.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static RuntimeDefinition Get(string? key) =>
        Find(key) ?? throw new DomainValidationException(
            nameof(App.Runtime),
            $"Runtime '{key}' is not supported. Supported runtimes: {string.Join(", ", All.Select(r => r.Key))}.");

    /// <summary>Display name for a stored key; falls back to the key if the runtime was since removed.</summary>
    public static string DisplayNameOf(string key) => Find(key)?.DisplayName ?? key;
}
