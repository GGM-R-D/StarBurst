using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEngine.Configuration;

public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => new Money(reader.GetDecimal()),
            JsonTokenType.String when decimal.TryParse(reader.GetString(), out var parsed) => new Money(parsed),
            _ => throw new JsonException("Money value must be numeric.")
        };
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Amount);
    }
}

