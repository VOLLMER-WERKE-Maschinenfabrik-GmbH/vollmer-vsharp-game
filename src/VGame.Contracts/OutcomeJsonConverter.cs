using System.Text.Json;
using System.Text.Json.Serialization;

namespace VGame.Contracts;

public sealed class OutcomeJsonConverter : JsonConverter<Outcome>
{
    public override Outcome Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "success" => Outcome.Success,
            "wrong_sharpening" => Outcome.WrongSharpening,
            "shaft_hit" => Outcome.ShaftHit,
            "miss" => Outcome.Miss,
            _ => throw new JsonException($"Unsupported action outcome '{value}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, Outcome value, JsonSerializerOptions options)
    {
        var serializedValue = value switch
        {
            Outcome.Success => "success",
            Outcome.WrongSharpening => "wrong_sharpening",
            Outcome.ShaftHit => "shaft_hit",
            Outcome.Miss => "miss",
            _ => throw new JsonException($"Unsupported action outcome '{value}'.")
        };

        writer.WriteStringValue(serializedValue);
    }
}