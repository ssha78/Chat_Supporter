using System;
using Newtonsoft.Json;

namespace ChatSupporter.Models;

// MessageType 문자열 변환기
public class MessageTypeConverter : JsonConverter<MessageType>
{
    public override void WriteJson(JsonWriter writer, MessageType value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }

    public override MessageType ReadJson(JsonReader reader, Type objectType, MessageType existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.Value is string stringValue)
        {
            return stringValue.ToLowerInvariant() switch
            {
                "customer" => MessageType.User,
                "user" => MessageType.User,
                "staff" => MessageType.Staff,
                "system" => MessageType.System,
                "ai" => MessageType.AI,
                "notification" => MessageType.Notification,
                _ => MessageType.User
            };
        }
        return MessageType.User;
    }
}

public class ChatMessage
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonProperty("sessionId")]
    public string SessionId { get; set; } = string.Empty;
    
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonProperty("sender")]
    public string Sender { get; set; } = string.Empty;
    
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [JsonProperty("type")]
    [JsonConverter(typeof(MessageTypeConverter))]
    public MessageType Type { get; set; } = MessageType.User;
    
    [JsonProperty("isFromStaff")]
    public bool IsFromStaff { get; set; } = false;
    
    [JsonProperty("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

public enum MessageType
{
    User,
    System,
    AI,
    Staff,
    Notification
}