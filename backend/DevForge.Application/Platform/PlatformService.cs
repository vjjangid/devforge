using DevForge.Application.Common;
using DevForge.Domain.Applications;
using Microsoft.Extensions.Options;

namespace DevForge.Application.Platform;

public sealed class PlatformService(IOptions<FeatureOptions> features)
{
    public PlatformInfoDto GetInfo() =>
        new(
            [.. RuntimeCatalog.All.Select(runtime => new RuntimeDto(runtime.Key, runtime.DisplayName))],
            new PlatformFeaturesDto(features.Value.FailureSimulation));
}
