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
    Offline,        // 오프라인
    Online,         // 온라인 대기
    Waiting,        // 직원 요청
    Active,         // 상담 진행 중
    Completed,      // 완료
    Disconnected    // 연결 끊김
}