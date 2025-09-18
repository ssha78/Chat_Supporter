using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ChatSupporter.Models;

namespace ChatSupporter.Services;

/// <summary>
/// SessionStatus enum을 JSON으로 직렬화/역직렬화하는 컨버터
/// </summary>
public class SessionStatusConverter : StringEnumConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is SessionStatus status)
        {
            writer.WriteValue(status.ToString());
        }
        else
        {
            base.WriteJson(writer, value, serializer);
        }
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            var stringValue = reader.Value?.ToString();
            if (!string.IsNullOrEmpty(stringValue))
            {
                if (Enum.TryParse<SessionStatus>(stringValue, true, out var result))
                {
                    return result;
                }
            }
        }

        return base.ReadJson(reader, objectType, existingValue, serializer);
    }
}