using System.Collections.Concurrent;
using GameEngine.Play;

namespace RGS.Services;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();

    public SessionRecord CreateSession(string operatorId, string gameId, string playerToken, bool funMode)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var record = new SessionRecord(
            sessionId,
            operatorId,
            gameId,
            playerToken,
            funMode,
            DateTimeOffset.UtcNow,
            EngineSessionState.Create());

        _sessions[sessionId] = record;
        return record;
    }

    public bool TryGetSession(string sessionId, out SessionRecord record) =>
        _sessions.TryGetValue(sessionId, out record!);

    public void UpdateState(string sessionId, EngineSessionState state)
    {
        if (!_sessions.TryGetValue(sessionId, out var record))
        {
            throw new InvalidOperationException("Session not found.");
        }

        record.State = state;
    }
}

public sealed class SessionRecord
{
    public SessionRecord(
        string sessionId,
        string operatorId,
        string gameId,
        string playerToken,
        bool funMode,
        DateTimeOffset createdAt,
        EngineSessionState state)
    {
        SessionId = sessionId;
        OperatorId = operatorId;
        GameId = gameId;
        PlayerToken = playerToken;
        FunMode = funMode;
        CreatedAt = createdAt;
        State = state;
    }

    public string SessionId { get; }
    public string OperatorId { get; }
    public string GameId { get; }
    public string PlayerToken { get; }
    public bool FunMode { get; }
    public DateTimeOffset CreatedAt { get; }
    public EngineSessionState State { get; set; }
}

