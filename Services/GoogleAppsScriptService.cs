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
    private readonly bool _isEnabled;

    public bool IsEnabled => _isEnabled;

    public GoogleAppsScriptService(string apiUrl, int maxRetries = 3, int timeoutSeconds = 30, bool isEnabled = true)
    {
        // URI 유효성 검증
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            throw new ArgumentException("API URL이 비어있습니다.", nameof(apiUrl));
        }

        _apiUrl = apiUrl.Trim();
        _maxRetries = maxRetries;
        _timeoutSeconds = timeoutSeconds;
        _isEnabled = isEnabled;

        if (!Uri.TryCreate(_apiUrl, UriKind.Absolute, out var uri))
        {
            throw new UriFormatException($"잘못된 API URL 형식: {_apiUrl}");
        }

        // HttpClientHandler로 리다이렉트 자동 처리 설정
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        };

        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ChatSupporter/1.0");
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
        progress?.Report($"API 요청 전송: {action} → {_apiUrl}");

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_apiUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    progress?.Report($"API 응답 내용: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}...");

                    var result = JsonConvert.DeserializeObject<ApiResponse<T>>(responseJson);

                    progress?.Report($"API 응답 성공: {action}");
                    return result ?? new ApiResponse<T> { Success = false, Message = "응답 파싱 실패" };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    progress?.Report($"API 오류 (시도 {attempt}/{_maxRetries}): {response.StatusCode} - {response.ReasonPhrase}");
                    if (attempt == _maxRetries)
                    {
                        return new ApiResponse<T>
                        {
                            Success = false,
                            Message = $"HTTP 오류: {response.StatusCode} {response.ReasonPhrase}\n응답: {errorContent?.Substring(0, Math.Min(200, errorContent?.Length ?? 0))}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"네트워크 오류 (시도 {attempt}/{_maxRetries}): {ex.GetType().Name} - {ex.Message}");
                if (attempt == _maxRetries)
                {
                    var detailMessage = ex is UriFormatException
                        ? $"URI 형식 오류: '{_apiUrl}' - {ex.Message}"
                        : $"네트워크 오류: {ex.Message}";

                    return new ApiResponse<T>
                    {
                        Success = false,
                        Message = detailMessage
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
        var result = await SendRequestAsync<List<ChatMessage>>("getChatHistory", new { sessionId }, progress);

        // API 실패시 테스트용 목업 데이터 반환
        if (!result.Success && sessionId.StartsWith("LM1234"))
        {
            progress?.Report("API 실패 - 테스트용 목업 데이터 사용");

            var mockMessages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Id = "msg1",
                    SessionId = sessionId,
                    Content = "화면이 제대로 보이지 않습니다.",
                    Sender = "LM1234",
                    Type = MessageType.User,
                    Timestamp = DateTime.Now.AddMinutes(-10),
                    IsFromStaff = false
                },
                new ChatMessage
                {
                    Id = "msg2",
                    SessionId = sessionId,
                    Content = "렌즈를 교체했는데도 같은 문제가 발생합니다.",
                    Sender = "LM1234",
                    Type = MessageType.User,
                    Timestamp = DateTime.Now.AddMinutes(-8),
                    IsFromStaff = false
                },
                new ChatMessage
                {
                    Id = "msg3",
                    SessionId = sessionId,
                    Content = "테스트 세션이 시작되었습니다. ID: " + sessionId,
                    Sender = "System",
                    Type = MessageType.System,
                    Timestamp = DateTime.Now.AddMinutes(-6),
                    IsFromStaff = false
                },
                new ChatMessage
                {
                    Id = "msg4",
                    SessionId = sessionId,
                    Content = "안녕하세요! 문제를 해결해드리겠습니다.",
                    Sender = "직원",
                    Type = MessageType.Staff,
                    Timestamp = DateTime.Now.AddMinutes(-4),
                    IsFromStaff = true
                },
                new ChatMessage
                {
                    Id = "msg5",
                    SessionId = sessionId,
                    Content = "감사합니다. 빠른 도움 부탁드립니다.",
                    Sender = "LM1234",
                    Type = MessageType.User,
                    Timestamp = DateTime.Now.AddMinutes(-2),
                    IsFromStaff = false
                }
            };

            return new ApiResponse<List<ChatMessage>>
            {
                Success = true,
                Message = "목업 데이터 로드됨",
                Data = mockMessages
            };
        }

        return result;
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
    
    // 활성 세션 목록 조회 (1시간 이내, 직원 요청 우선)
    public async Task<ApiResponse<List<ChatSession>>> GetActiveSessionsAsync(IProgress<string>? progress = null)
    {
        // 한국 시간 기준으로 1시간 전 계산
        var koreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        var koreaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, koreaTimeZone);
        var oneHourAgo = koreaTime.AddHours(-1);

        var requestData = new
        {
            fromDate = oneHourAgo.ToString("yyyy-MM-ddTHH:mm:ss"), // 한국 시간 기준
            excludeStatus = "Completed", // 완료된 세션 제외
            prioritizeStaffRequest = true, // 직원 요청 세션 상위 표시
            timeZone = "KST" // 한국 표준시 명시
        };

        progress?.Report($"1시간 필터 기준: {oneHourAgo:yyyy-MM-dd HH:mm:ss} KST");
        progress?.Report($"API URL: {_apiUrl}");

        var result = await SendRequestAsync<List<ChatSession>>("getActiveSessions", requestData, progress);

        // API 실패시 테스트용 목업 데이터 반환
        if (!result.Success)
        {
            progress?.Report("API 실패 - 테스트용 목업 세션 데이터 사용");

            var mockSessions = new List<ChatSession>
            {
                new ChatSession
                {
                    Id = "LM1234_CLAIM_20250117",
                    Customer = new Customer { SerialNumber = "LM1234", DeviceModel = "L-CAM_TEST" },
                    Status = SessionStatus.Online,
                    StartedAt = DateTime.Now.AddMinutes(-30),
                    LastActivity = DateTime.Now.AddMinutes(-5),
                    Messages = new List<ChatMessage>(),
                    AssignedStaff = ""
                },
                new ChatSession
                {
                    Id = "LM5678_CLAIM_20250117",
                    Customer = new Customer { SerialNumber = "LM5678", DeviceModel = "L-CAM_PRO" },
                    Status = SessionStatus.Waiting,
                    StartedAt = DateTime.Now.AddMinutes(-15),
                    LastActivity = DateTime.Now.AddMinutes(-2),
                    Messages = new List<ChatMessage>(),
                    AssignedStaff = ""
                }
            };

            return new ApiResponse<List<ChatSession>>
            {
                Success = true,
                Message = "목업 세션 데이터 로드됨",
                Data = mockSessions
            };
        }

        return result;
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
    
    // 새 세션 생성
    public async Task<ApiResponse<ChatSession>> CreateSessionAsync(ChatSession session, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<ChatSession>("createSession", session, progress);
    }

    // 세션 정보 업데이트
    public async Task<ApiResponse<ChatSession>> UpdateSessionAsync(ChatSession session, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<ChatSession>("updateSession", session, progress);
    }

    // 세션 상태 업데이트 (기존)
    public async Task<ApiResponse<bool>> UpdateSessionStatusAsync(string sessionId, SessionStatus status, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<bool>("updateSessionStatus", new { sessionId, status = status.ToString() }, progress);
    }

    // CustomerSession 관련 API 메서드들

    /// <summary>
    /// 고객 세션 조회 (단일 레코드)
    /// </summary>
    public async Task<ApiResponse<CustomerSession>> GetCustomerSessionAsync(string serialNumber, IProgress<string>? progress = null)
    {
        var result = await SendRequestAsync<CustomerSession>("getCustomerSession", new { serialNumber }, progress);

        // API 실패시 새 CustomerSession 반환 (신규 고객)
        if (!result.Success)
        {
            progress?.Report($"신규 고객 세션 생성: {serialNumber}");

            return new ApiResponse<CustomerSession>
            {
                Success = true,
                Message = "신규 고객 세션",
                Data = null // null을 반환하여 새 세션 생성 유도
            };
        }

        return result;
    }

    /// <summary>
    /// 고객 세션 생성 또는 업데이트
    /// </summary>
    public async Task<ApiResponse<CustomerSession>> CreateOrUpdateCustomerSessionAsync(CustomerSession session, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<CustomerSession>("createOrUpdateCustomerSession", session, progress);
    }

    /// <summary>
    /// 활성 고객 세션 목록 조회 (실시간 대시보드용)
    /// </summary>
    public async Task<ApiResponse<List<CustomerSession>>> GetActiveCustomerSessionsAsync(IProgress<string>? progress = null)
    {
        var result = await SendRequestAsync<List<CustomerSession>>("getActiveCustomerSessions", new { }, progress);

        // API 실패시 테스트용 목업 데이터 반환
        if (!result.Success)
        {
            progress?.Report("API 실패 - 테스트용 목업 고객 세션 데이터 사용");

            var mockCustomerSessions = new List<CustomerSession>
            {
                new CustomerSession
                {
                    SerialNumber = "LM1234",
                    DeviceModel = "L-CAM_TEST",
                    CurrentSessionId = "LM1234_SESSION_20250917203000",
                    Status = SessionStatus.Online,
                    IsOnline = true,
                    LastActivity = DateTime.UtcNow.AddMinutes(-2),
                    LastHeartbeat = DateTime.UtcNow.AddSeconds(-30),
                    Priority = SessionPriority.Normal,
                    TotalMessages = 5,
                    AssignedStaff = ""
                },
                new CustomerSession
                {
                    SerialNumber = "LM5678",
                    DeviceModel = "L-CAM_PRO",
                    CurrentSessionId = "LM5678_SESSION_20250917202800",
                    Status = SessionStatus.Waiting,
                    IsOnline = true,
                    LastActivity = DateTime.UtcNow.AddMinutes(-1),
                    LastHeartbeat = DateTime.UtcNow.AddSeconds(-45),
                    Priority = SessionPriority.High,
                    TotalMessages = 3,
                    AssignedStaff = ""
                },
                new CustomerSession
                {
                    SerialNumber = "LM9999",
                    DeviceModel = "L-CAM_BASIC",
                    CurrentSessionId = "LM9999_SESSION_20250917201500",
                    Status = SessionStatus.Active,
                    IsOnline = true,
                    LastActivity = DateTime.UtcNow.AddMinutes(-5),
                    LastHeartbeat = DateTime.UtcNow.AddMinutes(-6), // 비활성 상태
                    Priority = SessionPriority.Normal,
                    TotalMessages = 12,
                    AssignedStaff = "김직원"
                }
            };

            return new ApiResponse<List<CustomerSession>>
            {
                Success = true,
                Message = "목업 고객 세션 데이터 로드됨",
                Data = mockCustomerSessions
            };
        }

        return result;
    }

    /// <summary>
    /// 세션 히스토리 저장
    /// </summary>
    public async Task<ApiResponse<SessionHistory>> SaveSessionHistoryAsync(SessionHistory history, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<SessionHistory>("saveSessionHistory", history, progress);
    }

    /// <summary>
    /// 고객별 세션 히스토리 조회
    /// </summary>
    public async Task<ApiResponse<List<SessionHistory>>> GetCustomerSessionHistoryAsync(string serialNumber, int limit = 10, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<List<SessionHistory>>("getCustomerSessionHistory", new { serialNumber, limit }, progress);
    }

    /// <summary>
    /// 하트비트 업데이트 (경량화된 API)
    /// </summary>
    public async Task<ApiResponse<bool>> UpdateHeartbeatAsync(string serialNumber, IProgress<string>? progress = null)
    {
        return await SendRequestAsync<bool>("updateHeartbeat", new { serialNumber, timestamp = DateTime.UtcNow }, progress);
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