using DevForge.Application.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace DevForge.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <param name="todayStart">The caller's local midnight, so "deployments today" matches their day. Defaults to UTC midnight.</param>
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(
        [FromQuery] DateTimeOffset? todayStart,
        CancellationToken cancellationToken) =>
        Ok(await dashboard.GetSummaryAsync(todayStart, cancellationToken));
}
