using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ChatSupporter.Models;

public class ClaimCase
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonProperty("sessionId")]
    public string SessionId { get; set; } = string.Empty;
    
    [JsonProperty("customerSerialNumber")]
    public string CustomerSerialNumber { get; set; } = string.Empty;
    
    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;
    
    [JsonProperty("priority")]
    public string Priority { get; set; } = "보통";
    
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonProperty("status")]
    public ClaimStatus Status { get; set; } = ClaimStatus.Open;
    
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonProperty("closedAt")]
    public DateTime? ClosedAt { get; set; }
    
    [JsonProperty("assignedTo")]
    public string? AssignedTo { get; set; }
    
    [JsonProperty("resolution")]
    public string? Resolution { get; set; }
    
    [JsonProperty("attachments")]
    public List<string> Attachments { get; set; } = new();
    
    [JsonProperty("estimatedResolutionTime")]
    public TimeSpan? EstimatedResolutionTime { get; set; }
}

public enum ClaimStatus
{
    Open,
    InProgress,
    PendingCustomer,
    Resolved,
    Closed,
    Cancelled
}