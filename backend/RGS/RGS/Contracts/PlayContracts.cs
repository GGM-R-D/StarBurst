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

