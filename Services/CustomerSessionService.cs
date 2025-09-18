using ChatSupporter.Models;

namespace ChatSupporter.Services;

/// <summary>
/// 고객 세션 단일 레코드 관리 서비스
/// </summary>
public class CustomerSessionService
{
    private readonly GoogleAppsScriptService _apiService;
    private readonly ConfigurationService _configService;
    private readonly DebugLogService _debugLog;

    private CustomerSession? _currentCustomerSession;
    private readonly List<ChatMessage> _messageHistory = new();
    private readonly System.Threading.Timer _heartbeatTimer;
    private readonly System.Threading.Timer _syncTimer;

    public event EventHandler<ChatMessage>? MessageReceived;
    public event EventHandler<CustomerSession>? SessionStatusChanged;
    public event EventHandler<string>? StatusUpdate;

    public CustomerSession? CurrentSession => _currentCustomerSession;
    public IReadOnlyList<ChatMessage> MessageHistory => _messageHistory.AsReadOnly();

    public CustomerSessionService(GoogleAppsScriptService apiService, ConfigurationService configService, DebugLogService debugLog)
    {
        _apiService = apiService;
        _configService = configService;
        _debugLog = debugLog;

        // 하트비트 타이머 (30초마다)
        var heartbeatInterval = TimeSpan.FromSeconds(30);
        _heartbeatTimer = new System.Threading.Timer(SendHeartbeat, null, heartbeatInterval, heartbeatInterval);

        // 메시지 동기화 타이머 (기존과 동일)
        var refreshInterval = TimeSpan.FromSeconds(Math.Max(2, _configService.Settings.Chat.RefreshIntervalSeconds));
        _syncTimer = new System.Threading.Timer(SyncMessages, null, refreshInterval, refreshInterval);

        _debugLog.LogSession("서비스 초기화", $"CustomerSessionService 준비완료 - 하트비트: {heartbeatInterval.TotalSeconds}초, 동기화: {refreshInterval.TotalSeconds}초");
    }

    /// <summary>
    /// 고객 세션 시작 또는 재개
    /// </summary>
    public async Task<bool> StartOrResumeSessionAsync(string serialNumber, string deviceModel = "")
    {
        try
        {
            _debugLog.LogSession("세션 시작/재개", serialNumber);

            // 기존 CustomerSession 조회
            var existingSession = await GetCustomerSessionAsync(serialNumber);

            if (existingSession != null)
            {
                // 기존 세션 재개
                _currentCustomerSession = existingSession;
                _debugLog.LogSession("기존 세션 재개", serialNumber, $"상태: {existingSession.Status}");

                // 온라인 상태로 업데이트
                await UpdateSessionStatusAsync(SessionStatus.Online);
            }
            else
            {
                // 새 CustomerSession 생성 (임시로 ChatSession API 사용)
                _currentCustomerSession = new CustomerSession
                {
                    SerialNumber = serialNumber,
                    DeviceModel = deviceModel,
                    CurrentSessionId = GenerateSessionId(serialNumber),
                    Status = SessionStatus.Online,
                    IsOnline = true,
                    LastHeartbeat = DateTime.UtcNow,
                    ClientVersion = "1.0.0"
                };

                // 서버에 세션 즉시 생성 (시스템 메시지 전송으로)
                _debugLog.LogSession("새 세션 생성", serialNumber, $"세션 ID: {_currentCustomerSession.CurrentSessionId}");

                // 세션 시작을 알리는 시스템 메시지 전송하여 서버에 세션 생성 (화면에는 표시하지 않음)
                var systemMessage = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    SessionId = _currentCustomerSession.CurrentSessionId,
                    Content = $"세션 초기화 - {serialNumber}",
                    Sender = "System",
                    Type = MessageType.System,
                    Timestamp = DateTime.UtcNow,
                    IsFromStaff = false
                };

                // 서버에만 전송 (로컬 메시지 히스토리에는 추가하지 않음)
                var response = await _apiService.SendMessageAsync(systemMessage);
                if (response.Success)
                {
                    _debugLog.LogSession("서버 세션 생성", serialNumber, "성공");
                }
                else
                {
                    _debugLog.LogError("서버 세션 생성", response.Message);
                }
            }

            // 메시지 히스토리 로드
            await LoadMessageHistoryAsync(_currentCustomerSession.CurrentSessionId);

            SessionStatusChanged?.Invoke(this, _currentCustomerSession);
            StatusUpdate?.Invoke(this, $"세션 연결됨: {serialNumber}");

            return true;
        }
        catch (Exception ex)
        {
            _debugLog.LogError("세션 시작/재개 실패", ex.Message);
            StatusUpdate?.Invoke(this, $"세션 시작 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 직원이 고객 세션에 참여
    /// </summary>
    public async Task<bool> JoinCustomerSessionAsync(string serialNumber)
    {
        try
        {
            _debugLog.LogSession("직원 세션 참여", serialNumber);

            var customerSession = await GetCustomerSessionAsync(serialNumber);
            if (customerSession == null)
            {
                _debugLog.LogError("세션 참여 실패", $"고객 세션을 찾을 수 없음: {serialNumber}");
                return false;
            }

            _currentCustomerSession = customerSession;

            // 직원 배정 및 상태 업데이트
            customerSession.AssignedStaff = "직원"; // TODO: 실제 직원 ID
            customerSession.StaffAssignedAt = DateTime.UtcNow;
            customerSession.Status = SessionStatus.Active;

            await CreateOrUpdateCustomerSessionAsync(customerSession);

            // 메시지 히스토리 로드
            await LoadMessageHistoryAsync(customerSession.CurrentSessionId);

            _debugLog.LogSession("직원 세션 참여 완료", serialNumber, $"메시지 수: {_messageHistory.Count}");

            SessionStatusChanged?.Invoke(this, _currentCustomerSession);
            StatusUpdate?.Invoke(this, $"고객 세션 참여: {serialNumber}");

            return true;
        }
        catch (Exception ex)
        {
            _debugLog.LogError("직원 세션 참여 실패", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 메시지 전송
    /// </summary>
    public async Task<bool> SendMessageAsync(string content, MessageType type = MessageType.User, string? sender = null)
    {
        if (_currentCustomerSession == null)
        {
            _debugLog.LogError("메시지 전송", "활성 세션이 없습니다");
            return false;
        }

        try
        {
            _debugLog.LogMessage("메시지 전송 시작", sender ?? "Unknown", content, $"타입: {type}");

            var senderName = sender ?? (type == MessageType.Staff ? "직원" : _currentCustomerSession.SerialNumber);

            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = _currentCustomerSession.CurrentSessionId,
                Content = content,
                Sender = senderName,
                Type = type,
                Timestamp = DateTime.UtcNow,
                IsFromStaff = type == MessageType.Staff
            };

            // 로컬에 메시지 추가
            _messageHistory.Add(message);
            MessageReceived?.Invoke(this, message);

            // CustomerSession 업데이트
            _currentCustomerSession.LastActivity = DateTime.UtcNow;
            _currentCustomerSession.TotalMessages++;
            await CreateOrUpdateCustomerSessionAsync(_currentCustomerSession);

            _debugLog.LogMessage("로컬 메시지 추가", message.Sender, message.Content, $"ID: {message.Id}");

            // API 전송
            var response = await _apiService.SendMessageAsync(message);

            if (response.Success)
            {
                _debugLog.LogAPI("메시지 전송", "성공", message.Id);
                return true;
            }
            else
            {
                _debugLog.LogAPI("메시지 전송", "실패", response.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            _debugLog.LogError("메시지 전송 오류", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 세션 상태 업데이트
    /// </summary>
    public async Task<bool> UpdateSessionStatusAsync(SessionStatus status)
    {
        if (_currentCustomerSession == null) return false;

        try
        {
            var oldStatus = _currentCustomerSession.Status;
            _currentCustomerSession.Status = status;
            _currentCustomerSession.UpdatedAt = DateTime.UtcNow;

            await CreateOrUpdateCustomerSessionAsync(_currentCustomerSession);

            _debugLog.LogSession("상태 업데이트", _currentCustomerSession.SerialNumber, $"{oldStatus} → {status}");

            SessionStatusChanged?.Invoke(this, _currentCustomerSession);
            return true;
        }
        catch (Exception ex)
        {
            _debugLog.LogError("상태 업데이트 실패", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 하트비트 전송 (30초마다 자동 호출)
    /// </summary>
    private async void SendHeartbeat(object? state)
    {
        if (_currentCustomerSession == null) return;

        try
        {
            _currentCustomerSession.LastHeartbeat = DateTime.UtcNow;
            _currentCustomerSession.IsOnline = true;
            _currentCustomerSession.UpdatedAt = DateTime.UtcNow;

            await CreateOrUpdateCustomerSessionAsync(_currentCustomerSession);
            _debugLog.LogSession("하트비트 전송", _currentCustomerSession.SerialNumber);
        }
        catch (Exception ex)
        {
            _debugLog.LogError("하트비트 전송 실패", ex.Message);
        }
    }

    /// <summary>
    /// 메시지 동기화
    /// </summary>
    private async void SyncMessages(object? state)
    {
        if (_currentCustomerSession == null) return;

        try
        {
            var response = await _apiService.GetChatHistoryAsync(_currentCustomerSession.CurrentSessionId);

            if (response.Success && response.Data != null)
            {
                var newMessages = response.Data
                    .Where(m => !_messageHistory.Any(existing => existing.Id == m.Id))
                    .OrderBy(m => m.Timestamp);

                foreach (var message in newMessages)
                {
                    _messageHistory.Add(message);
                    MessageReceived?.Invoke(this, message);
                }

                if (newMessages.Any())
                {
                    _debugLog.LogSession("메시지 동기화", _currentCustomerSession.SerialNumber, $"{newMessages.Count()}개 새 메시지");
                }
            }
        }
        catch (Exception ex)
        {
            _debugLog.LogError("메시지 동기화 오류", ex.Message);
        }
    }

    /// <summary>
    /// 세션 종료 (명시적)
    /// </summary>
    public async Task EndSessionAsync(string reason = "세션 종료")
    {
        if (_currentCustomerSession == null) return;

        try
        {
            // 세션 히스토리 저장
            var sessionHistory = new SessionHistory
            {
                SerialNumber = _currentCustomerSession.SerialNumber,
                SessionId = _currentCustomerSession.CurrentSessionId,
                StartedAt = _currentCustomerSession.SessionStarted,
                EndedAt = DateTime.UtcNow,
                Status = _currentCustomerSession.Status.ToString(),
                AssignedStaff = _currentCustomerSession.AssignedStaff,
                ClaimId = _currentCustomerSession.CurrentClaimId,
                MessageCount = _messageHistory.Count,
                Duration = (int)(DateTime.UtcNow - _currentCustomerSession.SessionStarted).TotalSeconds,
                Resolution = reason
            };

            await SaveSessionHistoryAsync(sessionHistory);

            // CustomerSession 오프라인 상태로 업데이트
            _currentCustomerSession.Status = SessionStatus.Offline;
            _currentCustomerSession.IsOnline = false;
            _currentCustomerSession.UpdatedAt = DateTime.UtcNow;
            _currentCustomerSession.TotalSessions++;

            await CreateOrUpdateCustomerSessionAsync(_currentCustomerSession);

            // 서버의 ChatSession 상태를 Offline로 업데이트
            var statusUpdateResponse = await _apiService.UpdateSessionStatusAsync(_currentCustomerSession.CurrentSessionId, SessionStatus.Offline);
            if (statusUpdateResponse.Success)
            {
                _debugLog.LogSession("서버 세션 상태 업데이트", _currentCustomerSession.SerialNumber, "Offline으로 변경 성공");
            }
            else
            {
                _debugLog.LogError("서버 세션 상태 업데이트", statusUpdateResponse.Message);
            }

            // 서버에 세션 종료 알림 메시지 전송
            var offlineMessage = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = _currentCustomerSession.CurrentSessionId,
                Content = $"세션 종료 - {reason}",
                Sender = "System",
                Type = MessageType.System,
                Timestamp = DateTime.UtcNow,
                IsFromStaff = false
            };

            // 서버에만 전송 (로컬 메시지 히스토리에는 추가하지 않음)
            var messageResponse = await _apiService.SendMessageAsync(offlineMessage);
            if (messageResponse.Success)
            {
                _debugLog.LogSession("서버 종료 메시지 전송", _currentCustomerSession.SerialNumber, "성공");
            }
            else
            {
                _debugLog.LogError("서버 종료 메시지 전송", messageResponse.Message);
            }

            _debugLog.LogSession("세션 종료", _currentCustomerSession.SerialNumber, reason);

            // UI 업데이트를 위한 상태 변경 이벤트 발생
            var closedSession = _currentCustomerSession;
            _currentCustomerSession = null;
            _messageHistory.Clear();

            // 세션 종료 상태 알림
            SessionStatusChanged?.Invoke(this, null!);
            StatusUpdate?.Invoke(this, "세션이 종료되었습니다");
        }
        catch (Exception ex)
        {
            _debugLog.LogError("세션 종료 오류", ex.Message);
        }
    }

    // API 호출 메서드들
    private async Task<CustomerSession?> GetCustomerSessionAsync(string serialNumber)
    {
        // 임시로 기존 ChatSession API를 사용하여 CustomerSession으로 변환
        var response = await _apiService.GetCustomerSessionsAsync(serialNumber);
        if (response.Success && response.Data?.Any() == true)
        {
            var latestChatSession = response.Data
                .OrderByDescending(s => s.LastActivity)
                .FirstOrDefault();

            if (latestChatSession != null)
            {
                return new CustomerSession
                {
                    SerialNumber = serialNumber,
                    DeviceModel = latestChatSession.Customer?.DeviceModel ?? "Unknown",
                    CurrentSessionId = latestChatSession.Id,
                    Status = latestChatSession.Status,
                    IsOnline = latestChatSession.Status != SessionStatus.Offline,
                    LastActivity = latestChatSession.LastActivity,
                    SessionStarted = latestChatSession.StartedAt,
                    AssignedStaff = latestChatSession.AssignedStaff ?? "",
                    CurrentClaimId = latestChatSession.CurrentClaimId ?? "",
                    TotalMessages = latestChatSession.Messages?.Count ?? 0,
                    LastHeartbeat = DateTime.UtcNow,
                    Priority = SessionPriority.Normal,
                    CreatedAt = latestChatSession.StartedAt,
                    UpdatedAt = DateTime.UtcNow
                };
            }
        }
        return null;
    }

    private Task<bool> CreateOrUpdateCustomerSessionAsync(CustomerSession session)
    {
        // 임시로 기존 API 사용 (서버가 CustomerSession API 지원 시까지)
        // CustomerSession 업데이트는 로컬에서만 처리하고 성공으로 반환
        _debugLog.LogSession("CustomerSession 로컬 업데이트", session.SerialNumber, $"상태: {session.Status}");
        return Task.FromResult(true);
    }

    private async Task<bool> SaveSessionHistoryAsync(SessionHistory history)
    {
        var response = await _apiService.SaveSessionHistoryAsync(history);
        return response.Success;
    }

    private async Task LoadMessageHistoryAsync(string sessionId)
    {
        _debugLog.LogSession("메시지 히스토리 로딩 시작", sessionId);

        var response = await _apiService.GetChatHistoryAsync(sessionId);

        if (response.Success && response.Data != null)
        {
            _messageHistory.Clear();
            _messageHistory.AddRange(response.Data.OrderBy(m => m.Timestamp));

            _debugLog.LogSession("메시지 히스토리 로딩 완료", sessionId, $"{_messageHistory.Count}개 메시지");

            foreach (var message in _messageHistory)
            {
                MessageReceived?.Invoke(this, message);
            }
        }
    }

    /// <summary>
    /// 현재 세션을 직접 설정 (서버 조회 없이)
    /// </summary>
    public void SetCurrentSession(CustomerSession session)
    {
        _currentCustomerSession = session;
        _debugLog.LogSession("직접 세션 설정", session.SerialNumber, $"상태: {session.Status}");
        SessionStatusChanged?.Invoke(this, _currentCustomerSession);
    }

    /// <summary>
    /// 특정 세션의 메시지 히스토리 로드
    /// </summary>
    public async Task LoadMessageHistoryForSession(string sessionId)
    {
        await LoadMessageHistoryAsync(sessionId);
    }

    private string GenerateSessionId(string serialNumber)
    {
        return $"{serialNumber}_SESSION_{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    public void Dispose()
    {
        _heartbeatTimer?.Dispose();
        _syncTimer?.Dispose();
    }
}