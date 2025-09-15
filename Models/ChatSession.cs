using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ChatSupporter.Models;

public class ChatSession
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonProperty("customer")]
    public Customer? Customer { get; set; }
    
    [JsonProperty("messages")]
    public List<ChatMessage> Messages { get; set; } = new();
    
    [JsonProperty("status")]
    public SessionStatus Status { get; set; } = SessionStatus.Online;
    
    [JsonProperty("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    [JsonProperty("endedAt")]
    public DateTime? EndedAt { get; set; }
    
    [JsonProperty("assignedStaff")]
    public string? AssignedStaff { get; set; }
    
    [JsonProperty("lastActivity")]
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    
    [JsonProperty("attachmentRequested")]
    public bool AttachmentRequested { get; set; } = false;
    
    [JsonProperty("currentClaimId")]
    public string? CurrentClaimId { get; set; }
}

public enum SessionStatus
{
    Online,
    Waiting,
    Active,
    Completed,
    Disconnected
}