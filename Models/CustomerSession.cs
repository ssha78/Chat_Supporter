using System;
using Newtonsoft.Json;
using ChatSupporter.Services;

namespace ChatSupporter.Models;

/// <summary>
/// 고객별 단일 세션 레코드 - 실시간 상태 관리용
/// </summary>
public class CustomerSession
{
    [JsonProperty("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonProperty("deviceModel")]
    public string DeviceModel { get; set; } = string.Empty;

    // 현재 세션 정보
    [JsonProperty("currentSessionId")]
    public string CurrentSessionId { get; set; } = string.Empty;

    [JsonProperty("status")]
    [JsonConverter(typeof(SessionStatusConverter))]
    public SessionStatus Status { get; set; } = SessionStatus.Offline;

    [JsonProperty("priority")]
    public SessionPriority Priority { get; set; } = SessionPriority.Normal;

    // 타이밍 정보
    [JsonProperty("firstConnected")]
    public DateTime FirstConnected { get; set; } = DateTime.UtcNow;

    [JsonProperty("lastActivity")]
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    [JsonProperty("sessionStarted")]
    public DateTime SessionStarted { get; set; } = DateTime.UtcNow;

    [JsonProperty("estimatedCompletion")]
    public DateTime? EstimatedCompletion { get; set; }

    // 담당자 정보
    [JsonProperty("assignedStaff")]
    public string AssignedStaff { get; set; } = string.Empty;

    [JsonProperty("staffAssignedAt")]
    public DateTime? StaffAssignedAt { get; set; }

    // 클레임 정보
    [JsonProperty("currentClaimId")]
    public string CurrentClaimId { get; set; } = string.Empty;

    [JsonProperty("claimCategory")]
    public string ClaimCategory { get; set; } = string.Empty;

    [JsonProperty("claimDescription")]
    public string ClaimDescription { get; set; } = string.Empty;

    // 연결 상태
    [JsonProperty("isOnline")]
    public bool IsOnline { get; set; } = false;

    [JsonProperty("lastHeartbeat")]
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    [JsonProperty("clientVersion")]
    public string ClientVersion { get; set; } = "1.0.0";

    // 통계 정보
    [JsonProperty("totalSessions")]
    public int TotalSessions { get; set; } = 0;

    [JsonProperty("totalMessages")]
    public int TotalMessages { get; set; } = 0;

    [JsonProperty("averageResponseTime")]
    public int AverageResponseTime { get; set; } = 0;

    // 메타데이터
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 하트비트 체크용 헬퍼 메서드
    public bool IsActiveOnline => IsOnline &&
        (DateTime.UtcNow - LastHeartbeat).TotalMinutes < 5;

    public string GetDisplayStatus()
    {
        if (!IsOnline) return "오프라인";
        if (!IsActiveOnline) return "비활성";

        return Status switch
        {
            SessionStatus.Offline => "오프라인",
            SessionStatus.Online => "온라인",
            SessionStatus.Waiting => "직원 요청",
            SessionStatus.Active => "상담 중",
            SessionStatus.Completed => "완료",
            SessionStatus.Disconnected => "연결 끊김",
            _ => Status.ToString()
        };
    }
}

/// <summary>
/// 클레임 우선순위
/// </summary>
public enum SessionPriority
{
    Low,        // 낮음
    Normal,     // 보통
    High,       // 높음
    Urgent      // 긴급
}