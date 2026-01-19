namespace RGS.Contracts;

// Client-RGS compliant start request
public sealed record StartRequest(
    string LanguageId,
    string Client,
    int FunMode,
    string Token,
    string? CurrencyId = null); // Optional: only if funMode=1 and game has no default currency

public sealed record StartResponse(
    string SessionId,
    string GameId,
    string OperatorId,
    int FunMode,
    DateTimeOffset CreatedAt,
    string TimeSignature,
    string ThemeId);

