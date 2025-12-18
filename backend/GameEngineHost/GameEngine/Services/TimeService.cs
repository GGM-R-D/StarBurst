using System.Diagnostics;

namespace GameEngine.Services;

public interface ITimeService
{
    DateTimeOffset UtcNow { get; }
    long UnixMilliseconds { get; }
}

/// <summary>
/// Centralized, GLI-19 compliant time authority for both logging and transactional stamping.
/// </summary>
public sealed class TimeService : ITimeService
{
    private readonly TimeProvider _timeProvider;

    public TimeService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public long UnixMilliseconds => UtcNow.ToUnixTimeMilliseconds();
}

