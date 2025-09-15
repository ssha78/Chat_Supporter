using ChatSupporter.Models;

namespace ChatSupporter.Services;

public class SessionService
{
    private readonly GoogleAppsScriptService _apiService;
    private readonly ConfigurationService _configService;
    
    private ChatSession? _currentSession;
    private readonly List<ChatMessage> _messageHistory = new();
    private readonly System.Threading.Timer _syncTimer;

    public event EventHandler<ChatMessage>? MessageReceived;
    public event EventHandler<ChatSession>? SessionStatusChanged;
    public event EventHandler<string>? StatusUpdate;

    public ChatSession? CurrentSession => _currentSession;
    public IReadOnlyList<ChatMessage> MessageHistory => _messageHistory.AsReadOnly();

    public SessionService(GoogleAppsScriptService apiService, ConfigurationService configService)
    {
        _apiService = apiService;
        _configService = configService;
        
        var refreshInterval = TimeSpan.FromSeconds(_configService.Settings.Chat.RefreshIntervalSeconds);
        _syncTimer = new System.Threading.Timer(SyncMessages, null, refreshInterval, refreshInterval);
    }

    public Task<bool> StartSessionAsync(string serialNumber, Customer? customer = null)
    {
        try
        {
            var sessionId = GenerateSessionId(serialNumber);
            
            _currentSession = new ChatSession
            {
                Id = sessionId,
                Customer = customer ?? new Customer { SerialNumber = serialNumber },
                Status = SessionStatus.Online,
                StartedAt = DateTime.UtcNow
            };

            StatusUpdate?.Invoke(this, "세션 시작됨");
            SessionStatusChanged?.Invoke(this, _currentSession);
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(this, $"세션 시작 실패: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> SendMessageAsync(string content, MessageType type = MessageType.User, string? sender = null)
    {
        if (_currentSession == null)
        {
            StatusUpdate?.Invoke(this, "활성 세션이 없습니다");
            return false;
        }

        try
        {
            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = _currentSession.Id,
                Content = content,
                Sender = sender ?? (_currentSession.Customer?.SerialNumber ?? "Unknown"),
                Type = type,
                Timestamp = DateTime.UtcNow,
                IsFromStaff = type == MessageType.Staff
            };

            var response = await _apiService.SendMessageAsync(message);
            
            if (response.Success)
            {
                _messageHistory.Add(message);
                MessageReceived?.Invoke(this, message);
                _currentSession.LastActivity = DateTime.UtcNow;
                return true;
            }
            else
            {
                StatusUpdate?.Invoke(this, $"메시지 전송 실패: {response.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
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
            var response = await _apiService.GetChatHistoryAsync(_currentSession.Id);
            
            if (response.Success && response.Data != null)
            {
                var newMessages = response.Data
                    .Where(m => !_messageHistory.Any(existing => existing.Id == m.Id))
                    .OrderBy(m => m.Timestamp);

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
                }
            }
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(this, $"메시지 동기화 오류: {ex.Message}");
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

    private string GenerateSessionId(string serialNumber)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var guid = Guid.NewGuid().ToString()[..8];
        return $"{serialNumber}_{timestamp}_{guid}";
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
    }
}