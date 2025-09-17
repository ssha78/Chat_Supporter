using ChatSupporter.Models;

namespace ChatSupporter.Services;

public class SessionService
{
    private readonly GoogleAppsScriptService _apiService;
    private readonly ConfigurationService _configService;
    private readonly DebugLogService _debugLog;

    private ChatSession? _currentSession;
    private readonly List<ChatMessage> _messageHistory = new();
    private readonly System.Threading.Timer _syncTimer;

    public event EventHandler<ChatMessage>? MessageReceived;
    public event EventHandler<ChatSession>? SessionStatusChanged;
    public event EventHandler<string>? StatusUpdate;

    public ChatSession? CurrentSession => _currentSession;
    public IReadOnlyList<ChatMessage> MessageHistory => _messageHistory.AsReadOnly();

    public SessionService(GoogleAppsScriptService apiService, ConfigurationService configService, DebugLogService debugLog)
    {
        _apiService = apiService;
        _configService = configService;
        _debugLog = debugLog;

        _debugLog.LogSession("SessionService 초기화", "시작");

        var refreshInterval = TimeSpan.FromSeconds(Math.Max(2, _configService.Settings.Chat.RefreshIntervalSeconds)); // 최소 2초
        _syncTimer = new System.Threading.Timer(SyncMessages, null, refreshInterval, refreshInterval);

        _debugLog.LogSession("SessionService 초기화", $"완료 - 동기화 간격: {refreshInterval.TotalSeconds}초");
    }

    public async Task<bool> StartSessionAsync(string serialNumber, Customer? customer = null)
    {
        try
        {
            // 먼저 해당 시리얼 번호의 활성 세션이 있는지 확인
            var existingSession = await GetActiveSessionBySerialNumberAsync(serialNumber);

            if (existingSession != null)
            {
                // 기존 활성 세션 재개
                _currentSession = existingSession;
                StatusUpdate?.Invoke(this, $"기존 세션 재개: {existingSession.Id}");

                // 기존 메시지 히스토리 로드
                await LoadMessageHistoryAsync(_currentSession.Id);
            }
            else
            {
                // 새 세션 생성 (시리얼 번호를 세션 ID로 사용)
                var sessionId = GenerateClaimBasedSessionId(serialNumber);

                _currentSession = new ChatSession
                {
                    Id = sessionId,
                    Customer = customer ?? new Customer { SerialNumber = serialNumber },
                    Status = SessionStatus.Online,
                    StartedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                };

                // 서버에 새 세션 생성
                var createResponse = await _apiService.CreateSessionAsync(_currentSession);
                if (createResponse.Success)
                {
                    StatusUpdate?.Invoke(this, $"새 클레임 세션 생성됨: {sessionId}");
                }
                else
                {
                    StatusUpdate?.Invoke(this, $"세션 생성 실패: {createResponse.Message} (로컬에서 계속)");
                }
            }

            SessionStatusChanged?.Invoke(this, _currentSession);
            return true;
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(this, $"세션 시작 실패: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> JoinExistingSessionAsync(ChatSession existingSession)
    {
        try
        {
            _debugLog.LogSession("세션 참여 시작", existingSession.Id, $"고객: {existingSession.Customer?.SerialNumber}");

            // 기존 세션을 현재 세션으로 설정
            _currentSession = existingSession;

            // 기존 메시지 히스토리 로드
            await LoadMessageHistoryAsync(_currentSession.Id);

            _debugLog.LogSession("세션 참여 완료", _currentSession.Id, $"메시지 수: {_messageHistory.Count}");

            StatusUpdate?.Invoke(this, $"기존 세션 참여: {_currentSession.Id}");
            SessionStatusChanged?.Invoke(this, _currentSession);

            return true;
        }
        catch (Exception ex)
        {
            _debugLog.LogError("세션 참여 실패", ex.Message);
            StatusUpdate?.Invoke(this, $"세션 참여 실패: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendMessageAsync(string content, MessageType type = MessageType.User, string? sender = null)
    {
        if (_currentSession == null)
        {
            _debugLog.LogError("메시지 전송", "활성 세션이 없습니다");
            StatusUpdate?.Invoke(this, "활성 세션이 없습니다");
            return false;
        }

        try
        {
            _debugLog.LogMessage("메시지 전송 시작", sender ?? "Unknown", content, $"타입: {type}");

            // 한국 시간으로 메시지 타임스탬프 설정
            var koreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
            var koreaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, koreaTimeZone);

            var senderName = sender ?? (type == MessageType.Staff ? "직원" : (_currentSession.Customer?.SerialNumber ?? "Unknown"));

            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = _currentSession.Id,
                Content = content,
                Sender = senderName,
                Type = type,
                Timestamp = koreaTime, // 한국 시간으로 설정
                IsFromStaff = type == MessageType.Staff
            };

            _debugLog.LogMessage("메시지 생성", senderName, content, $"타입: {type}, ID: {message.Id}");

            // 먼저 로컬에 메시지 추가 (사용자 경험 개선)
            _messageHistory.Add(message);
            MessageReceived?.Invoke(this, message);
            _currentSession.LastActivity = DateTime.UtcNow;

            _debugLog.LogMessage("로컬 메시지 추가", message.Sender, message.Content, $"ID: {message.Id}");

            var response = await _apiService.SendMessageAsync(message);

            if (response.Success)
            {
                _debugLog.LogAPI("메시지 전송", "성공", message.Id);
                StatusUpdate?.Invoke(this, "메시지 서버 전송 완료");
                return true;
            }
            else
            {
                _debugLog.LogAPI("메시지 전송", "실패", response.Message);
                StatusUpdate?.Invoke(this, $"서버 전송 실패: {response.Message}");
                // 서버 전송은 실패했지만 로컬 표시는 성공
                return false;
            }
        }
        catch (Exception ex)
        {
            _debugLog.LogError("메시지 전송 오류", ex.Message);
            StatusUpdate?.Invoke(this, $"메시지 전송 오류: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateSessionStatusAsync(SessionStatus status)
    {
        if (_currentSession == null) return false;

        try
        {
            var response = await _apiService.UpdateSessionStatusAsync(_currentSession.Id, status);
            
            if (response.Success)
            {
                _currentSession.Status = status;
                SessionStatusChanged?.Invoke(this, _currentSession);
                
                var statusText = status switch
                {
                    SessionStatus.Online => "온라인",
                    SessionStatus.Waiting => "직원 요청",
                    SessionStatus.Active => "활성",
                    SessionStatus.Completed => "완료",
                    SessionStatus.Disconnected => "연결 해제",
                    _ => status.ToString()
                };
                
                StatusUpdate?.Invoke(this, $"세션 상태 변경: {statusText}");
                return true;
            }
            else
            {
                StatusUpdate?.Invoke(this, $"상태 업데이트 실패: {response.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(this, $"상태 업데이트 오류: {ex.Message}");
            return false;
        }
    }

    private async void SyncMessages(object? state)
    {
        if (_currentSession == null) return;

        try
        {
            StatusUpdate?.Invoke(this, $"메시지 동기화 중... (세션: {_currentSession.Id})");
            var response = await _apiService.GetChatHistoryAsync(_currentSession.Id);

            if (response.Success && response.Data != null)
            {
                var newMessages = response.Data
                    .Where(m => !_messageHistory.Any(existing => existing.Id == m.Id))
                    .OrderBy(m => m.Timestamp);

                var newMessageCount = newMessages.Count();
                StatusUpdate?.Invoke(this, $"새 메시지 {newMessageCount}개 발견");

                foreach (var message in newMessages)
                {
                    if (message.Timestamp.Kind == DateTimeKind.Utc)
                    {
                        message.Timestamp = message.Timestamp.ToLocalTime();
                    }
                    else if (message.Timestamp.Kind == DateTimeKind.Unspecified)
                    {
                        message.Timestamp = DateTime.SpecifyKind(message.Timestamp, DateTimeKind.Utc).ToLocalTime();
                    }

                    _messageHistory.Add(message);
                    MessageReceived?.Invoke(this, message);
                    StatusUpdate?.Invoke(this, $"새 메시지 수신: {message.Sender} - {message.Content}");
                }

                if (newMessageCount == 0)
                {
                    StatusUpdate?.Invoke(this, "새 메시지 없음");
                }
            }
            else
            {
                StatusUpdate?.Invoke(this, $"메시지 동기화 실패: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(this, $"메시지 동기화 오류: {ex.Message}");
        }
    }

    // 수동 메시지 동기화
    public async Task ManualSyncMessagesAsync()
    {
        await Task.Run(() => SyncMessages(null));
    }

    public async Task<bool> CompleteClaimAsync(string reason = "클레임 해결됨")
    {
        if (_currentSession == null) return false;

        try
        {
            // 클레임 완료 메시지 추가
            await SendMessageAsync($"[시스템] {reason}", MessageType.System);

            // 세션 정보 업데이트
            _currentSession.Status = SessionStatus.Completed;
            _currentSession.EndedAt = DateTime.UtcNow;
            _currentSession.LastActivity = DateTime.UtcNow;

            // 서버에 세션 업데이트
            var updateResponse = await _apiService.UpdateSessionAsync(_currentSession);
            if (updateResponse.Success)
            {
                StatusUpdate?.Invoke(this, $"클레임 완료: {reason}");
                return true;
            }
            else
            {
                StatusUpdate?.Invoke(this, $"서버 업데이트 실패: {updateResponse.Message} (로컬 완료됨)");
                return true; // 로컬에서는 완료된 상태
            }
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(this, $"클레임 완료 오류: {ex.Message}");
            return false;
        }
    }

    public void EndSession()
    {
        if (_currentSession != null)
        {
            _currentSession.Status = SessionStatus.Completed;
            _currentSession.EndedAt = DateTime.UtcNow;
            SessionStatusChanged?.Invoke(this, _currentSession);
        }

        _currentSession = null;
        _messageHistory.Clear();
        StatusUpdate?.Invoke(this, "세션 종료됨");
    }

    private string GenerateClaimBasedSessionId(string serialNumber)
    {
        // 클레임 기반 세션 ID: 시리얼번호_CLAIM_날짜
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        return $"{serialNumber}_CLAIM_{date}";
    }

    private async Task<ChatSession?> GetActiveSessionBySerialNumberAsync(string serialNumber)
    {
        try
        {
            var response = await _apiService.GetActiveSessionsAsync();

            if (response.Success && response.Data != null)
            {
                return response.Data.FirstOrDefault(s =>
                    s.Customer?.SerialNumber == serialNumber &&
                    s.Status != SessionStatus.Completed);
            }
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(this, $"활성 세션 조회 오류: {ex.Message}");
        }

        return null;
    }

    private async Task LoadMessageHistoryAsync(string sessionId)
    {
        try
        {
            _debugLog.LogSession("메시지 히스토리 로딩 시작", sessionId);
            StatusUpdate?.Invoke(this, $"메시지 히스토리 로딩 시작: {sessionId}");

            var progress = new Progress<string>(message =>
            {
                _debugLog.LogAPI("히스토리 API", "진행", message);
                StatusUpdate?.Invoke(this, message);
            });

            var response = await _apiService.GetChatHistoryAsync(sessionId, progress);

            if (response.Success && response.Data != null)
            {
                _debugLog.LogAPI("히스토리 로딩", "성공", $"{response.Data.Count}개 메시지");
                StatusUpdate?.Invoke(this, $"서버에서 {response.Data.Count}개 메시지 수신됨");
                _messageHistory.Clear();

                foreach (var message in response.Data.OrderBy(m => m.Timestamp))
                {
                    if (message.Timestamp.Kind == DateTimeKind.Utc)
                    {
                        message.Timestamp = message.Timestamp.ToLocalTime();
                    }
                    else if (message.Timestamp.Kind == DateTimeKind.Unspecified)
                    {
                        message.Timestamp = DateTime.SpecifyKind(message.Timestamp, DateTimeKind.Utc).ToLocalTime();
                    }

                    _messageHistory.Add(message);
                    MessageReceived?.Invoke(this, message);
                    _debugLog.LogMessage("히스토리 메시지 로드", message.Sender, message.Content, $"ID: {message.Id}");
                    StatusUpdate?.Invoke(this, $"메시지 로드: {message.Sender} - {message.Content.Substring(0, Math.Min(30, message.Content.Length))}...");
                }

                _debugLog.LogSession("메시지 히스토리 로딩 완료", sessionId, $"{_messageHistory.Count}개 메시지");
                StatusUpdate?.Invoke(this, $"메시지 히스토리 로드 완료: {_messageHistory.Count}개");
            }
            else
            {
                _debugLog.LogAPI("히스토리 로딩", "실패", response.Message);
                StatusUpdate?.Invoke(this, $"메시지 히스토리 로드 실패: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            _debugLog.LogError("히스토리 로드 오류", ex.Message);
            StatusUpdate?.Invoke(this, $"히스토리 로드 오류: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
    }
}