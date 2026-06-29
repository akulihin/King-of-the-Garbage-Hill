using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace King_of_the_Garbage_Hill.API.Controllers;

[ApiController]
[Route("api/widget")]
[EnableCors]
public class WidgetController : ControllerBase
{
    private readonly DiscordWidgetService _widgetService;

    public WidgetController(DiscordWidgetService widgetService)
    {
        _widgetService = widgetService;
    }

    public sealed class SyncRequest
    {
        public string AccessToken { get; set; }
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncRequest body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.AccessToken))
            return BadRequest(new { error = "missing accessToken" });

        var success = await _widgetService.TryVerifyAndAuthorizeAsync(body.AccessToken);
        return success
            ? Ok(new { ok = true })
            : BadRequest(new { error = "verification or sync failed" });
    }
}
