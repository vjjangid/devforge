using DevForge.Application.Platform;
using Microsoft.AspNetCore.Mvc;

namespace DevForge.Api.Controllers;

[ApiController]
[Route("api/platform")]
[Produces("application/json")]
public sealed class PlatformController(PlatformService platform) : ControllerBase
{
    /// <summary>Supported runtimes and enabled features, so the UI does not hardcode either.</summary>
    [HttpGet]
    public ActionResult<PlatformInfoDto> Get() => Ok(platform.GetInfo());
}
