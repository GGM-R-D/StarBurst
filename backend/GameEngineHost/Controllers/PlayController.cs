using GameEngine.Play;
using GameEngineHost.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameEngineHost.Controllers;

/// <summary>
/// Play endpoint controller per RGS-Game Server specification.
/// Handles /play POST requests with proper error responses.
/// </summary>
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
        
        // Log with both bet (per RGS spec) and totalBet fields
        var effectiveBet = request.Bet ?? request.TotalBet;
        logger.LogInformation("Play request received: GameId={GameId}, Bet={Bet}, TotalBet={TotalBet}, BetMode={BetMode}, HasLastResponse={HasLastResponse}", 
            request.GameId, 
            request.Bet?.Amount ?? 0, 
            request.TotalBet.Amount, 
            request.BetMode,
            request.LastResponse.HasValue);
        
        try
        {
            logger.LogInformation("Calling engine client PlayAsync...");
            var response = await _engineClient.PlayAsync(request, cancellationToken);
            
            // Log using updated field names
            logger.LogInformation("Play response generated: Win={Win}, FreeSpins={FreeSpins}, RoundId={RoundId}, HasFeature={HasFeature}", 
                response.Win.Amount, 
                response.FreeSpins,  // Renamed from FreeSpinsAwarded
                response.RoundId,
                response.Feature != null);
                
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid argument in play request");
            // RGS spec: error responses should have statusCode and message
            return BadRequest(new { 
                statusCode = 400, 
                message = ex.Message, 
                parameter = ex.ParamName 
            });
        }
        catch (FileNotFoundException ex)
        {
            logger.LogError(ex, "Configuration file not found");
            return StatusCode(500, new { 
                statusCode = 500, 
                message = $"Configuration file not found: {ex.Message}" 
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Invalid operation in play request");
            return StatusCode(500, new { 
                statusCode = 500, 
                message = ex.Message 
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing play request");
            return StatusCode(500, new { 
                statusCode = 500, 
                message = "An error occurred processing the request", 
                details = ex.Message 
            });
        }
    }
}

