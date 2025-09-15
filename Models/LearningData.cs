using System;
using Newtonsoft.Json;

namespace ChatSupporter.Models;

public class LearningData
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [JsonProperty("question")]
    public string Question { get; set; } = string.Empty;
    
    [JsonProperty("answer")]
    public string Answer { get; set; } = string.Empty;
    
    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;
    
    [JsonProperty("confidence")]
    public double Confidence { get; set; } = 0.0;
    
    [JsonProperty("usageCount")]
    public int UsageCount { get; set; } = 0;
    
    [JsonProperty("feedback")]
    public string? Feedback { get; set; }
    
    [JsonProperty("sessionId")]
    public string? SessionId { get; set; }
    
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonProperty("isVerified")]
    public bool IsVerified { get; set; } = false;
    
    [JsonProperty("tags")]
    public string[]? Tags { get; set; }
}