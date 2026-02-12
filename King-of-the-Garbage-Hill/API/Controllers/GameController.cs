using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.API.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace King_of_the_Garbage_Hill.API.Controllers;

/// <summary>
/// REST API for game actions. Alternative to SignalR for clients that prefer HTTP.
/// All endpoints require the Discord ID in the X-Discord-Id header.
/// </summary>
[ApiController]
[Route("api/game")]
[EnableCors]
public class GameController : ControllerBase
{
    private readonly WebGameService _gameService;

    public GameController(WebGameService gameService)
    {
        _gameService = gameService;
    }

    // ── Queries ───────────────────────────────────────────────────────

    [HttpGet("lobby")]
    public IActionResult GetLobby()
    {
        return Ok(_gameService.GetLobbyState());
    }

    [HttpGet("{gameId}")]
    public IActionResult GetGameState(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var state = _gameService.GetGameState(gameId, discordId);
        return state != null ? Ok(state) : NotFound(new { error = "Game not found" });
    }

    [HttpGet("{gameId}/spectate")]
    public IActionResult SpectateGame(ulong gameId)
    {
        var state = _gameService.GetGameStateForSpectator(gameId);
        return state != null ? Ok(state) : NotFound(new { error = "Game not found" });
    }

    // ── Actions ───────────────────────────────────────────────────────

    [HttpPost("{gameId}/attack")]
    public async Task<IActionResult> Attack(ulong gameId, [FromBody] AttackRequest req)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.Attack(gameId, discordId, req.TargetPlace);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/block")]
    public async Task<IActionResult> Block(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.Block(gameId, discordId);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/auto-move")]
    public async Task<IActionResult> AutoMove(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.AutoMove(gameId, discordId);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/change-mind")]
    public async Task<IActionResult> ChangeMind(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.ChangeMind(gameId, discordId);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/confirm-skip")]
    public async Task<IActionResult> ConfirmSkip(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.ConfirmSkip(gameId, discordId);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/confirm-predict")]
    public async Task<IActionResult> ConfirmPredict(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.ConfirmPredict(gameId, discordId);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/level-up")]
    public async Task<IActionResult> LevelUp(ulong gameId, [FromBody] LevelUpRequest req)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.LevelUp(gameId, discordId, req.StatIndex);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/moral-to-points")]
    public async Task<IActionResult> MoralToPoints(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.MoralToPoints(gameId, discordId);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/moral-to-skill")]
    public async Task<IActionResult> MoralToSkill(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.MoralToSkill(gameId, discordId);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/predict")]
    public async Task<IActionResult> Predict(ulong gameId, [FromBody] PredictRequest req)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.Predict(gameId, discordId, req.TargetPlayerId, req.CharacterName);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/aram-reroll")]
    public async Task<IActionResult> AramReroll(ulong gameId, [FromBody] AramRerollRequest req)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.AramReroll(gameId, discordId, req.Slot);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    [HttpPost("{gameId}/aram-confirm")]
    public async Task<IActionResult> AramConfirm(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return Unauthorized(new { error = "Missing X-Discord-Id header" });

        var (success, error) = await _gameService.AramConfirm(gameId, discordId);
        return success ? Ok(new { success = true }) : BadRequest(new { error });
    }

    // ── Helper ────────────────────────────────────────────────────────

    private ulong GetDiscordId()
    {
        if (Request.Headers.TryGetValue("X-Discord-Id", out var values) &&
            ulong.TryParse(values.ToString(), out var id))
            return id;
        return 0;
    }
}
