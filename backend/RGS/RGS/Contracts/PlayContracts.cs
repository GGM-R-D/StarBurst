using System.Text.Json;

namespace RGS.Contracts;

public sealed class ClientPlayRequest
{
    public required string SessionId { get; init; }
    public required decimal BaseBet { get; init; }
    public string BetMode { get; init; } = "standard";
    public required IReadOnlyList<ClientBetRequest> Bets { get; init; }
    public JsonElement? UserPayload { get; init; }
    public JsonElement? LastResponse { get; init; }
    // When present, overrides session fun mode so cheat mode works per user selection (0 = real, 1 = fun/demo)
    public int? FunMode { get; init; }
    // Cheat/debug fields: forwarded to engine when present (only honored by engine in FunMode)
    public bool DebugEnabled { get; init; }
    public int[]? Stops { get; init; }
    public JsonElement? Cheat { get; init; }
    public int[]? Multipliers { get; init; }
}

public sealed class BuyFeatureRequest
{
    public required string SessionId { get; init; }
    public required decimal BaseBet { get; init; }
    public string BetMode { get; init; } = "standard";
    public IReadOnlyList<ClientBetRequest>? Bets { get; init; }
    public JsonElement? UserPayload { get; init; }
}

public sealed record ClientBetRequest(string BetType, decimal Amount);

