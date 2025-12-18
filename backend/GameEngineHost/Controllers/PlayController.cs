using GameEngine.Play;
using GameEngineHost.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameEngineHost.Controllers;

[ApiController]
[Route("play")]
public sealed class PlayController : ControllerBase
{
    private readonly IEngineClient _engineClient;

    public PlayController(IEngineClient engineClient)
    {
        _engineClient = engineClient;
    }

    [HttpPost]
    public async Task<ActionResult<PlayResponse>> Play([FromBody] PlayRequest request, CancellationToken cancellationToken)
    {
        var logger = HttpContext.RequestServices.GetRequiredService<ILogger<PlayController>>();
        logger.LogInformation("Play request received: GameId={GameId}, TotalBet={TotalBet}, BetMode={BetMode}", 
            request.GameId, request.TotalBet.Amount, request.BetMode);
        
        try
        {
            logger.LogInformation("Calling engine client PlayAsync...");
            var response = await _engineClient.PlayAsync(request, cancellationToken);
            logger.LogInformation("Play response generated: Win={Win}, RoundId={RoundId}", 
                response.Win.Amount, response.RoundId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid argument in play request");
            return BadRequest(new { error = ex.Message, parameter = ex.ParamName });
        }
        catch (FileNotFoundException ex)
        {
            logger.LogError(ex, "Configuration file not found");
            return StatusCode(500, new { error = $"Configuration file not found: {ex.Message}" });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Invalid operation in play request");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing play request");
            return StatusCode(500, new { error = "An error occurred processing the request", details = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}

