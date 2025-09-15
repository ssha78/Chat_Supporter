using System;
using Newtonsoft.Json;

namespace ChatSupporter.Models;

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