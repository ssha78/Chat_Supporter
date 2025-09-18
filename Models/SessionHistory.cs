using System;
using Newtonsoft.Json;

namespace ChatSupporter.Models;

/// <summary>
/// 완료된 세션들의 히스토리 기록
/// </summary>
public class SessionHistory
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonProperty("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonProperty("startedAt")]
    public DateTime StartedAt { get; set; }

    [JsonProperty("endedAt")]
    public DateTime? EndedAt { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("assignedStaff")]
    public string AssignedStaff { get; set; } = string.Empty;

    [JsonProperty("claimId")]
    public string ClaimId { get; set; } = string.Empty;

    [JsonProperty("messageCount")]
    public int MessageCount { get; set; } = 0;

    [JsonProperty("duration")]
    public int Duration { get; set; } = 0; // 초 단위

    [JsonProperty("resolution")]
    public string Resolution { get; set; } = string.Empty;

    [JsonProperty("customerSatisfaction")]
    public int? CustomerSatisfaction { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 헬퍼 메서드들
    public TimeSpan GetDurationTimeSpan()
    {
        return TimeSpan.FromSeconds(Duration);
    }

    public string GetFormattedDuration()
    {
        var timeSpan = GetDurationTimeSpan();
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}시간 {timeSpan.Minutes}분";
        else
            return $"{timeSpan.Minutes}분 {timeSpan.Seconds}초";
    }
}