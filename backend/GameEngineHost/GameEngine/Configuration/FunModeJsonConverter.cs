using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEngine.Configuration;

/// <summary>
/// Deserializes funMode from JSON as either boolean (true/false) or number (1/0)
/// so the engine works when the client or RGS sends "funMode": 1.
/// </summary>
public sealed class FunModeJsonConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt64(out long n) => n != 0,
            JsonTokenType.Number when reader.TryGetDouble(out double d) => d != 0,
            _ => throw new JsonException($"Unexpected token type {reader.TokenType} for funMode (expected true/false or 0/1).")
        };
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}
