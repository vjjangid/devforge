using DevForge.Domain.Deployments;

namespace DevForge.Application.Deployments;

/// <summary>Turns a deployment's status and stage into the step list the UI renders.</summary>
public static class PipelineView
{
    public const string QueuedStep = "Queued";
    public const string CompletedStep = "Completed";

    public static IReadOnlyList<PipelineStepDto> For(DeploymentStatus status, DeploymentStage? currentStage)
    {
        var steps = new List<PipelineStepDto>(DeploymentPipeline.Stages.Count + 2)
        {
            new(QueuedStep, status == DeploymentStatus.Queued ? PipelineStepState.Active : PipelineStepState.Completed),
        };

        steps.AddRange(DeploymentPipeline.Stages.Select(
            stage => new PipelineStepDto(stage.ToString(), StageState(stage, status, currentStage))));

        steps.Add(new PipelineStepDto(
            CompletedStep,
            status == DeploymentStatus.Succeeded ? PipelineStepState.Completed : PipelineStepState.Pending));

        return steps;
    }

    private static PipelineStepState StageState(DeploymentStage stage, DeploymentStatus status, DeploymentStage? currentStage)
    {
        if (currentStage is null || stage > currentStage)
        {
            return PipelineStepState.Pending;
        }

        if (stage < currentStage)
        {
            return PipelineStepState.Completed;
        }

        return status switch
        {
            DeploymentStatus.Running => PipelineStepState.Active,
            DeploymentStatus.Succeeded => PipelineStepState.Completed,
            _ => PipelineStepState.Failed,
        };
    }
}
