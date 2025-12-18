using System.Collections.Concurrent;
using GameEngine.Play;

namespace RGS.Services;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();

    public SessionRecord CreateSession(string operatorId, string gameId, string playerToken, bool funMode, decimal initialBalance = 10000m)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var record = new SessionRecord(
            sessionId,
            operatorId,
            gameId,
            playerToken,
            funMode,
            DateTimeOffset.UtcNow,
            EngineSessionState.Create(),
            initialBalance);

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

    public void UpdateBalance(string sessionId, decimal bet, decimal win)
    {
        if (!_sessions.TryGetValue(sessionId, out var record))
        {
            throw new InvalidOperationException("Session not found.");
        }

        // Deduct bet and add win
        record.Balance = record.Balance - bet + win;
    }

    public decimal GetBalance(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var record))
        {
            return 10000m; // Default balance if session not found
        }

        return record.Balance;
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
        EngineSessionState state,
        decimal initialBalance = 10000m)
    {
        SessionId = sessionId;
        OperatorId = operatorId;
        GameId = gameId;
        PlayerToken = playerToken;
        FunMode = funMode;
        CreatedAt = createdAt;
        State = state;
        Balance = initialBalance;
    }

    public string SessionId { get; }
    public string OperatorId { get; }
    public string GameId { get; }
    public string PlayerToken { get; }
    public bool FunMode { get; }
    public DateTimeOffset CreatedAt { get; }
    public EngineSessionState State { get; set; }
    public decimal Balance { get; set; }
}

