using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cooldown.Core.Models;

/// <summary>
/// Reads "assignments" as either the old { "730": "bucketId" } shape or the new
/// { "730": ["bucketId", "other"] } shape (a game can belong to more than one bucket now),
/// so existing config.json files keep working without a manual edit. Always writes the new shape.
/// </summary>
public sealed class AssignmentsJsonConverter : JsonConverter<Dictionary<int, List<string>>>
{
    public override Dictionary<int, List<string>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected an object for assignments.");

        var result = new Dictionary<int, List<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return result;

            var appId = int.Parse(reader.GetString()!);
            reader.Read();

            List<string> buckets = reader.TokenType switch
            {
                JsonTokenType.String => new List<string> { reader.GetString()! },
                JsonTokenType.StartArray => ReadArray(ref reader),
                _ => throw new JsonException($"Unexpected token for assignment value: {reader.TokenType}"),
            };
            if (buckets.Count > 0)
                result[appId] = buckets;
        }
        throw new JsonException("Unexpected end of JSON while reading assignments.");
    }

    private static List<string> ReadArray(ref Utf8JsonReader reader)
    {
        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            list.Add(reader.GetString()!);
        return list;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<int, List<string>> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (appId, buckets) in value)
        {
            writer.WritePropertyName(appId.ToString());
            writer.WriteStartArray();
            foreach (var bucketId in buckets)
                writer.WriteStringValue(bucketId);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}
