using DevForge.Application.Deployments;
using DevForge.Domain.Deployments;

namespace DevForge.UnitTests.Application;

public class PipelineViewTests
{
    private const PipelineStepState Pending = PipelineStepState.Pending;
    private const PipelineStepState Active = PipelineStepState.Active;
    private const PipelineStepState Completed = PipelineStepState.Completed;
    private const PipelineStepState Failed = PipelineStepState.Failed;

    [Fact]
    public void Lists_queued_then_every_stage_then_completed()
    {
        var names = PipelineView.For(DeploymentStatus.Queued, currentStage: null).Select(step => step.Name);

        Assert.Equal(["Queued", "Preparing", "Building", "Testing", "Deploying", "Completed"], names);
    }

    [Fact]
    public void A_queued_deployment_is_waiting_on_the_first_step()
    {
        AssertStates(DeploymentStatus.Queued, null, Active, Pending, Pending, Pending, Pending, Pending);
    }

    [Fact]
    public void A_running_deployment_highlights_its_current_stage()
    {
        AssertStates(DeploymentStatus.Running, DeploymentStage.Building, Completed, Completed, Active, Pending, Pending, Pending);
    }

    [Fact]
    public void A_succeeded_deployment_completes_every_step()
    {
        AssertStates(DeploymentStatus.Succeeded, DeploymentStage.Deploying, Completed, Completed, Completed, Completed, Completed, Completed);
    }

    [Fact]
    public void A_failed_deployment_marks_the_stage_that_failed_and_leaves_the_rest_pending()
    {
        AssertStates(DeploymentStatus.Failed, DeploymentStage.Testing, Completed, Completed, Completed, Failed, Pending, Pending);
    }

    private static void AssertStates(DeploymentStatus status, DeploymentStage? stage, params PipelineStepState[] expected)
    {
        Assert.Equal(expected, PipelineView.For(status, stage).Select(step => step.State));
    }
}
