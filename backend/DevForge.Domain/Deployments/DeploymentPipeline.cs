namespace DevForge.Domain.Deployments;

public static class DeploymentPipeline
{
    private static readonly DeploymentStage[] Ordered = Enum.GetValues<DeploymentStage>();

    public static IReadOnlyList<DeploymentStage> Stages => Ordered;

    public static DeploymentStage FirstStage => Ordered[0];

    /// <summary>The stage that must follow <paramref name="current"/>, or <c>null</c> after the last one.</summary>
    public static DeploymentStage? NextAfter(DeploymentStage? current)
    {
        if (current is null)
        {
            return FirstStage;
        }

        var index = Array.IndexOf(Ordered, current.Value);
        return index < Ordered.Length - 1 ? Ordered[index + 1] : null;
    }
}
