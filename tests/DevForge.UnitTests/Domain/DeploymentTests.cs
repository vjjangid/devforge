using DevForge.Domain.Common;
using DevForge.Domain.Deployments;

namespace DevForge.UnitTests.Domain;

public class DeploymentTests
{
    private const string WorkerId = "worker-1";
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Queue_creates_a_queued_deployment_with_a_version()
    {
        var deployment = Deployment.Queue(Guid.NewGuid(), number: 3, simulateFailure: true, Now);

        Assert.Equal(DeploymentStatus.Queued, deployment.Status);
        Assert.Equal("v3", deployment.Version);
        Assert.True(deployment.SimulateFailure);
        Assert.Null(deployment.CurrentStage);
        Assert.Null(deployment.StartedAt);
        Assert.Null(deployment.CompletedAt);
    }

    [Fact]
    public void Start_moves_a_queued_deployment_to_running()
    {
        var deployment = Queued();

        deployment.Start(WorkerId, Now.AddSeconds(1));

        Assert.Equal(DeploymentStatus.Running, deployment.Status);
        Assert.Equal(WorkerId, deployment.WorkerId);
        Assert.Equal(Now.AddSeconds(1), deployment.StartedAt);
    }

    [Fact]
    public void Start_cannot_be_called_twice()
    {
        var deployment = Running();

        Assert.Throws<InvalidStateTransitionException>(() => deployment.Start("worker-2", Now));
    }

    [Fact]
    public void Stages_must_be_entered_in_pipeline_order()
    {
        var deployment = Running();

        Assert.Throws<InvalidStateTransitionException>(() => deployment.EnterStage(DeploymentStage.Building));

        deployment.EnterStage(DeploymentStage.Preparing);
        Assert.Throws<InvalidStateTransitionException>(() => deployment.EnterStage(DeploymentStage.Preparing));
        Assert.Throws<InvalidStateTransitionException>(() => deployment.EnterStage(DeploymentStage.Testing));

        deployment.EnterStage(DeploymentStage.Building);
        Assert.Equal(DeploymentStage.Building, deployment.CurrentStage);
    }

    [Fact]
    public void A_queued_deployment_cannot_enter_a_stage()
    {
        Assert.Throws<InvalidStateTransitionException>(() => Queued().EnterStage(DeploymentStage.Preparing));
    }

    [Fact]
    public void Succeed_requires_every_stage_to_have_run()
    {
        var deployment = Running();
        deployment.EnterStage(DeploymentStage.Preparing);

        Assert.Throws<InvalidStateTransitionException>(() => deployment.Succeed(Now));

        deployment.EnterStage(DeploymentStage.Building);
        deployment.EnterStage(DeploymentStage.Testing);
        deployment.EnterStage(DeploymentStage.Deploying);
        deployment.Succeed(Now.AddMinutes(1));

        Assert.Equal(DeploymentStatus.Succeeded, deployment.Status);
        Assert.Equal(Now.AddMinutes(1), deployment.CompletedAt);
        Assert.Null(deployment.ErrorMessage);
    }

    [Fact]
    public void Fail_records_the_error_and_keeps_the_stage_that_failed()
    {
        var deployment = Running();
        deployment.EnterStage(DeploymentStage.Preparing);
        deployment.EnterStage(DeploymentStage.Building);

        deployment.Fail("Compilation failed", Now.AddSeconds(30));

        Assert.Equal(DeploymentStatus.Failed, deployment.Status);
        Assert.Equal(DeploymentStage.Building, deployment.CurrentStage);
        Assert.Equal("Compilation failed", deployment.ErrorMessage);
        Assert.Equal(Now.AddSeconds(30), deployment.CompletedAt);
    }

    [Fact]
    public void A_finished_deployment_cannot_change_state()
    {
        var deployment = Running();
        deployment.Fail("boom", Now);

        Assert.Throws<InvalidStateTransitionException>(() => deployment.Fail("again", Now));
        Assert.Throws<InvalidStateTransitionException>(() => deployment.Succeed(Now));
        Assert.Throws<InvalidStateTransitionException>(() => deployment.EnterStage(DeploymentStage.Preparing));
    }

    [Fact]
    public void Fail_truncates_an_overlong_error_message()
    {
        var deployment = Running();

        deployment.Fail(new string('x', Deployment.ErrorMessageMaxLength + 50), Now);

        Assert.Equal(Deployment.ErrorMessageMaxLength, deployment.ErrorMessage!.Length);
    }

    private static Deployment Queued() => Deployment.Queue(Guid.NewGuid(), number: 1, simulateFailure: false, Now);

    private static Deployment Running()
    {
        var deployment = Queued();
        deployment.Start(WorkerId, Now);
        return deployment;
    }
}
