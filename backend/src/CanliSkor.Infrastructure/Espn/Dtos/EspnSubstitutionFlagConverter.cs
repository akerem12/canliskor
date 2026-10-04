using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanliSkor.Infrastructure.Espn.Dtos;

/// <summary>
/// Reads "was this player substituted" in either shape ESPN uses: a plain true/false, or an object such as
/// {"didSub": true}. Anything else counts as false, so one odd value can't fail the whole match.
/// </summary>
internal sealed class EspnSubstitutionFlagConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.StartObject:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    return document.RootElement.TryGetProperty("didSub", out var didSub) && didSub.ValueKind == JsonValueKind.True;
                }
            default:
                reader.Skip();
                return false;
        }
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
}
