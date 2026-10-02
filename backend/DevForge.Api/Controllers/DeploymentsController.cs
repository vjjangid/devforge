using DevForge.Application.Deployments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DevForge.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class DeploymentsController(DeploymentService deployments) : ControllerBase
{
    [HttpGet("applications/{applicationId:guid}/deployments")]
    [ProducesResponseType<IReadOnlyList<DeploymentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DeploymentDto>>> ListForApplication(Guid applicationId, CancellationToken cancellationToken) =>
        Ok(await deployments.ListForApplicationAsync(applicationId, cancellationToken));

    /// <summary>Queues a deployment and returns immediately; a worker processes it asynchronously.</summary>
    [HttpPost("applications/{applicationId:guid}/deployments")]
    [ProducesResponseType<DeploymentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeploymentDto>> Create(
        Guid applicationId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateDeploymentRequest? request,
        CancellationToken cancellationToken)
    {
        var created = await deployments.CreateAsync(applicationId, request ?? new CreateDeploymentRequest(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("deployments/{id:guid}")]
    [ProducesResponseType<DeploymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeploymentDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await deployments.GetAsync(id, cancellationToken));

    /// <param name="afterId">Only return log entries with an id greater than this (for incremental polling).</param>
    [HttpGet("deployments/{id:guid}/logs")]
    [ProducesResponseType<IReadOnlyList<DeploymentLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DeploymentLogDto>>> GetLogs(
        Guid id,
        [FromQuery] long? afterId,
        CancellationToken cancellationToken) =>
        Ok(await deployments.GetLogsAsync(id, afterId, cancellationToken));
}
