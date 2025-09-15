using System;
using Newtonsoft.Json;

namespace ChatSupporter.Models;

public class StaffNote
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [JsonProperty("customerSerialNumber")]
    public string CustomerSerialNumber { get; set; } = string.Empty;
    
    [JsonProperty("sessionId")]
    public string? SessionId { get; set; }
    
    [JsonProperty("staffName")]
    public string StaffName { get; set; } = string.Empty;
    
    [JsonProperty("note")]
    public string Note { get; set; } = string.Empty;
    
    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;
    
    [JsonProperty("priority")]
    public string Priority { get; set; } = "보통";
    
    [JsonProperty("isPrivate")]
    public bool IsPrivate { get; set; } = false;
    
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonProperty("tags")]
    public string[]? Tags { get; set; }
    
    [JsonProperty("relatedClaimId")]
    public string? RelatedClaimId { get; set; }
}