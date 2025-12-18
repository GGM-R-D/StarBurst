namespace GameEngine.Play;

public interface IGameEngineService
{
    Task<PlayResponse> PlayAsync(PlayRequest request, CancellationToken cancellationToken = default);
}

public sealed class GameEngineService : IGameEngineService
{
    private readonly SpinHandler _spinHandler;

    public GameEngineService(SpinHandler spinHandler)
    {
        _spinHandler = spinHandler;
    }

    public Task<PlayResponse> PlayAsync(PlayRequest request, CancellationToken cancellationToken = default) =>
        _spinHandler.PlayAsync(request, cancellationToken);
}

