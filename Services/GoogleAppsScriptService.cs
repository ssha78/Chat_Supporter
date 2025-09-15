using System.Text;
using ChatSupporter.Models;
using Newtonsoft.Json;

namespace ChatSupporter.Services;

public class GoogleAppsScriptService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly int _maxRetries;
    private readonly int _timeoutSeconds;

    public GoogleAppsScriptService(string apiUrl, int maxRetries = 3, int timeoutSeconds = 30)
    {
        _apiUrl = apiUrl;
        _maxRetries = maxRetries;
        _timeoutSeconds = timeoutSeconds;
        
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);
    }

    public async Task<ApiResponse<T>> SendRequestAsync<T>(string action, object data, IProgress<string>? progress = null)
    {
        var request = new
        {
            action = action,
            data = data,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        var json = JsonConvert.SerializeObject(request);
        progress?.Report($"API 요청 전송: {action}");

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_apiUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse<T>>(responseJson);
                    
                    progress?.Report($"API 응답 성공: {action}");
                    return result ?? new ApiResponse<T> { Success = false, Message = "응답 파싱 실패" };
                }
                else
                {
                    progress?.Report($"API 오류 (시도 {attempt}/{_maxRetries}): {response.StatusCode}");
                    if (attempt == _maxRetries)
                    {
                        return new ApiResponse<T>
                        {
                            Success = false,
                            Message = $"HTTP 오류: {response.StatusCode}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"네트워크 오류 (시도 {attempt}/{_maxRetries}): {ex.Message}");
                if (attempt == _maxRetries)
                {
                    return new ApiResponse<T>
                    {
                        Success = false,
                        Message = $"네트워크 오류: {ex.Message}"
                    };
                }
            }

            await Task.Delay(1000 * attempt);
        }

        return new ApiResponse<T> { Success = false, Message = "최대 재시도 횟수 초과" };
    }

    // 채팅 메시지 전송
    public async Task<ApiResponse<ChatMessage>> SendMessageAsync(ChatMessage message, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<ChatMessage>("sendMessage", message, progress);
    }

    // 채팅 히스토리 조회
    public async Task<ApiResponse<List<ChatMessage>>> GetChatHistoryAsync(string sessionId, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<List<ChatMessage>>("getChatHistory", new { sessionId }, progress);
    }

    // 최신 클레임 ID 기반으로 채팅 히스토리 로드
    public async Task<ApiResponse<List<ChatMessage>>> GetChatHistoryByLatestClaimAsync(string sessionId, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<List<ChatMessage>>("getChatHistoryByLatestClaim", new { sessionId }, progress);
    }

    // 클레임 생성
    public async Task<ApiResponse<ClaimCase>> CreateClaimAsync(ClaimCase claim, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<ClaimCase>("createClaim", claim, progress);
    }

    // 클레임 조회
    public async Task<ApiResponse<ClaimCase>> GetClaimAsync(string claimId, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<ClaimCase>("getClaim", new { claimId }, progress);
    }

    // 학습 데이터 저장
    public async Task<ApiResponse<LearningData>> SaveLearningDataAsync(LearningData learningData, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<LearningData>("saveLearningData", learningData, progress);
    }

    // AI 답변 요청
    public async Task<ApiResponse<string>> GetAIResponseAsync(string question, string category, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<string>("getAIResponse", new { question, category }, progress);
    }
    
    // 활성 세션 목록 조회
    public async Task<ApiResponse<List<ChatSession>>> GetActiveSessionsAsync(IProgress<string>? progress = null)
    {
        return await SendRequestAsync<List<ChatSession>>("getActiveSessions", new { }, progress);
    }
    
    // 학습 데이터 목록 조회 (ML 모델 학습용)
    public async Task<ApiResponse<List<LearningData>>> GetLearningDataAsync(int limit = 100, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<List<LearningData>>("getLearningData", new { limit }, progress);
    }
    
    // 고객별 세션 이력 조회
    public async Task<ApiResponse<List<ChatSession>>> GetCustomerSessionsAsync(string serialNumber, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<List<ChatSession>>("getCustomerSessions", new { serialNumber }, progress);
    }
    
    // 고객별 직원 메모 조회
    public async Task<ApiResponse<List<StaffNote>>> GetCustomerNotesAsync(string serialNumber, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<List<StaffNote>>("getCustomerNotes", new { serialNumber }, progress);
    }
    
    // 고객별 클레임 이력 조회
    public async Task<ApiResponse<List<ClaimCase>>> GetCustomerClaimsAsync(string serialNumber, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<List<ClaimCase>>("getCustomerClaims", new { serialNumber }, progress);
    }

    // 클레임 완료/닫기 처리
    public async Task<ApiResponse<ClaimCase>> CompleteClaimAsync(string claimId, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<ClaimCase>("completeClaim", new { claimId }, progress);
    }
    
    // 직원 메모 저장
    public async Task<ApiResponse<StaffNote>> SaveStaffNoteAsync(StaffNote note, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<StaffNote>("saveStaffNote", note, progress);
    }
    
    // 첨부 요청 상태 업데이트
    public async Task<ApiResponse<bool>> UpdateAttachmentRequestAsync(string sessionId, bool attachmentRequested, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<bool>("updateAttachmentRequest", new { sessionId, attachmentRequested }, progress);
    }
    
    // 세션 첨부 상태 조회
    public async Task<ApiResponse<bool>> GetAttachmentRequestStatusAsync(string sessionId, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<bool>("getAttachmentRequestStatus", new { sessionId }, progress);
    }
    
    // 세션 상태 업데이트
    public async Task<ApiResponse<bool>> UpdateSessionStatusAsync(string sessionId, SessionStatus status, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<bool>("updateSessionStatus", new { sessionId, status = status.ToString() }, progress);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}