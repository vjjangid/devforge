using DevForge.Application.Applications;
using Microsoft.AspNetCore.Mvc;

namespace DevForge.Api.Controllers;

[ApiController]
[Route("api/applications")]
[Produces("application/json")]
public sealed class ApplicationsController(ApplicationService applications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> List(CancellationToken cancellationToken) =>
        Ok(await applications.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await applications.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> Create(CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        var created = await applications.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> Update(Guid id, UpdateApplicationRequest request, CancellationToken cancellationToken) =>
        Ok(await applications.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await applications.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
