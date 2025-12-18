using System.Net.Http.Json;
using GameEngine.Play;

namespace GameEngineHost.Services;

public interface IEngineClient
{
    Task<PlayResponse> PlayAsync(PlayRequest request, CancellationToken cancellationToken);
}

public sealed class LocalEngineClient : IEngineClient
{
    private readonly SpinHandler _spinHandler;

    public LocalEngineClient(SpinHandler spinHandler)
    {
        _spinHandler = spinHandler;
    }

    public Task<PlayResponse> PlayAsync(PlayRequest request, CancellationToken cancellationToken) =>
        _spinHandler.PlayAsync(request, cancellationToken);
}

