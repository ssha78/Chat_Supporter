using ChatSupporter.Models;
using ChatSupporter.Services;

namespace ChatSupporter.Forms;

public partial class MainForm : Form
{
    private readonly ConfigurationService _configService;
    private readonly GoogleAppsScriptService _apiService;
    private readonly CustomerSessionService _sessionService;
    private readonly DeviceDetectionService _deviceService;
    private readonly DebugLogService _debugLog;

    private SessionManagerForm? _sessionManager;
    private bool _isStaffMode = false;
    private string _currentSerialNumber = string.Empty;

    public MainForm()
    {
        InitializeComponent();

        // 디버깅 로그 초기화 (Customer 모드로 시작)
        _debugLog = new DebugLogService("Customer");
        _debugLog.LogUI("앱 시작", "MainForm 초기화 시작");

        _configService = new ConfigurationService();
        _apiService = new GoogleAppsScriptService(
            _configService.Settings.GoogleAppsScript.ChatApiUrl,
            _configService.Settings.GoogleAppsScript.MaxRetries,
            _configService.Settings.GoogleAppsScript.TimeoutSeconds,
            _configService.Settings.GoogleAppsScript.Enabled
        );
        _sessionService = new CustomerSessionService(_apiService, _configService, _debugLog);
        _deviceService = new DeviceDetectionService();

        SetupForm();
        SetupEvents();
        DetectDevicesAndStartSession();

        _debugLog.LogUI("앱 시작", "MainForm 초기화 완료");
    }

    private void SetupForm()
    {
        this.Text = "Chat Supporter";
        this.Size = new Size(_configService.Settings.UI.WindowWidth, _configService.Settings.UI.WindowHeight);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(600, 400);
    }

    private void SetupEvents()
    {
        _sessionService.MessageReceived += OnMessageReceived;
        _sessionService.SessionStatusChanged += OnSessionStatusChanged;
        _sessionService.StatusUpdate += OnStatusUpdate;
        _deviceService.DeviceDetected += OnDeviceDetected;
    }

    private void DetectDevicesAndStartSession()
    {
        var devices = _deviceService.DetectedDevices;
        if (devices.Any())
        {
            var device = devices.First();
            _currentSerialNumber = device.SerialNumber;
            
            _ = _sessionService.StartOrResumeSessionAsync(_currentSerialNumber, device.DeviceModel);
            
            StatusLabel.Text = $"장치 연결됨: {device.SerialNumber}";
            StatusLabel.ForeColor = Color.Green;
        }
        else
        {
            StatusLabel.Text = "장치를 찾는 중...";
            StatusLabel.ForeColor = Color.Orange;
        }
    }

    private void OnDeviceDetected(object? sender, DeviceInfo device)
    {
        this.Invoke(() =>
        {
            if (string.IsNullOrEmpty(_currentSerialNumber))
            {
                _currentSerialNumber = device.SerialNumber;
                
                _ = _sessionService.StartOrResumeSessionAsync(_currentSerialNumber, device.DeviceModel);
                
                StatusLabel.Text = $"장치 연결됨: {device.SerialNumber}";
                StatusLabel.ForeColor = Color.Green;
            }
        });
    }

    private void OnMessageReceived(object? sender, ChatMessage message)
    {
        this.Invoke(() =>
        {
            AddMessageToChat(message);
        });
    }

    private void OnSessionStatusChanged(object? sender, CustomerSession session)
    {
        this.Invoke(() =>
        {
            var statusText = session.Status switch
            {
                SessionStatus.Online => "온라인",
                SessionStatus.Waiting => "직원 요청",
                SessionStatus.Active => "활성",
                SessionStatus.Completed => "완료",
                SessionStatus.Disconnected => "연결 해제",
                _ => session.Status.ToString()
            };

            SessionStatusLabel.Text = $"세션 상태: {statusText}";

            // 클레임 완료 버튼 상태 업데이트
            if (session.Status == SessionStatus.Completed)
            {
                CompleteClaimButton.Enabled = false;
                CompleteClaimButton.Text = "완료됨";
                CompleteClaimButton.BackColor = Color.LightGray;
            }
            else if (session.Status == SessionStatus.Online || session.Status == SessionStatus.Active)
            {
                CompleteClaimButton.Enabled = true;
                CompleteClaimButton.Text = "클레임 완료";
                CompleteClaimButton.BackColor = Color.Gold;
            }
        });
    }

    private void OnStatusUpdate(object? sender, string status)
    {
        this.Invoke(() =>
        {
            StatusLabel.Text = status;
        });
    }

    private void AddMessageToChat(ChatMessage message)
    {
        var displayText = $"[{message.Timestamp:HH:mm}] ";

        if (message.Type == MessageType.System)
        {
            displayText += "[시스템] ";
        }
        else if (message.Type == MessageType.Staff || message.IsFromStaff)
        {
            displayText += "[직원] ";
        }
        else if (message.Type == MessageType.User)
        {
            displayText += "[고객] ";
        }
        else
        {
            // 기타 메시지 타입이나 fallback
            displayText += $"[{message.Sender}] ";
        }

        displayText += message.Content;

        _debugLog.LogUI("메시지 표시", $"타입: {message.Type}, IsFromStaff: {message.IsFromStaff}, 표시: {displayText}");
        
        ChatListBox.Items.Add(displayText);
        ChatListBox.TopIndex = ChatListBox.Items.Count - 1;
    }

    private async void SendButton_Click(object? sender, EventArgs e)
    {
        var message = MessageTextBox.Text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        MessageTextBox.Text = string.Empty;

        // 전송 버튼 비활성화
        SendButton.Enabled = false;
        SendButton.Text = "전송중...";

        try
        {
            var messageType = _isStaffMode ? MessageType.Staff : MessageType.User;
            var success = await _sessionService.SendMessageAsync(message, messageType, _isStaffMode ? "직원" : _currentSerialNumber);

            if (!success)
            {
                MessageBox.Show($"서버 전송에 실패했습니다.\n(메시지는 채팅창에 표시됨)\n\n상태: {StatusLabel.Text}", "서버 전송 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // 메시지는 이미 채팅창에 표시되었으므로 복원하지 않음
            }
        }
        finally
        {
            // 전송 버튼 복구
            SendButton.Enabled = true;
            SendButton.Text = "전송";
        }
    }

    private void MessageTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)Keys.Enter)
        {
            // Shift+Enter는 줄바꿈, 일반 Enter는 전송
            if (Control.ModifierKeys != Keys.Shift)
            {
                e.Handled = true;
                SendButton_Click(sender, e);
            }
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Ctrl+S로 수동 동기화 (CustomerSessionService에서는 자동 동기화)
        if (keyData == (Keys.Control | Keys.S))
        {
            StatusLabel.Text = "동기화는 자동으로 실행됩니다";
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async void ConnectToStaffButton_Click(object? sender, EventArgs e)
    {
        if (_sessionService.CurrentSession?.Status == SessionStatus.Online)
        {
            var success = await _sessionService.UpdateSessionStatusAsync(SessionStatus.Waiting);
            if (success)
            {
                await _sessionService.SendMessageAsync("고객이 직원 연결을 요청했습니다.", MessageType.System, "시스템");
                ConnectToStaffButton.Enabled = false;
                ConnectToStaffButton.Text = "직원 요청됨";
            }
        }
    }

    private void StaffModeButton_Click(object? sender, EventArgs e)
    {
        _isStaffMode = !_isStaffMode;

        if (_isStaffMode)
        {
            _debugLog.LogUI("모드 전환", "직원 모드로 전환");
            StaffModeButton.Text = "고객 모드";
            StaffModeButton.BackColor = Color.Orange;
            ConnectToStaffButton.Visible = false;

            // 로그 모드를 Staff로 변경
            var staffDebugLog = new DebugLogService("Staff");
            // SessionService에 새 로그 서비스 설정 (실제로는 SessionService 재생성이 필요하지만 임시로 현재 로그 사용)

            ShowSessionManager();
        }
        else
        {
            _debugLog.LogUI("모드 전환", "고객 모드로 전환");
            StaffModeButton.Text = "직원 모드";
            StaffModeButton.BackColor = SystemColors.Control;
            ConnectToStaffButton.Visible = true;

            HideSessionManager();
        }
    }

    private async void TestSessionButton_Click(object? sender, EventArgs e)
    {
        if (_sessionService.CurrentSession != null)
        {
            await _sessionService.EndSessionAsync("테스트 세션 종료");
            TestSessionButton.Text = "테스트 세션";
            TestSessionButton.BackColor = Color.LightGreen;
            CompleteClaimButton.Enabled = false;
        }
        else
        {
            _currentSerialNumber = "LM1234";

            var success = await _sessionService.StartOrResumeSessionAsync(_currentSerialNumber, "L-CAM_TEST");
            if (success)
            {
                TestSessionButton.Text = "세션 종료";
                TestSessionButton.BackColor = Color.LightCoral;
                CompleteClaimButton.Enabled = true;
                StatusLabel.Text = $"테스트 세션 시작됨: {_currentSerialNumber}";
                StatusLabel.ForeColor = Color.Green;
            }
            else
            {
                StatusLabel.Text = "세션 시작 실패";
                StatusLabel.ForeColor = Color.Red;
            }
        }
    }

    private async void CompleteClaimButton_Click(object? sender, EventArgs e)
    {
        if (_sessionService.CurrentSession == null) return;

        var result = MessageBox.Show(
            "클레임을 완료하시겠습니까?\n완료된 클레임은 더 이상 수정할 수 없습니다.",
            "클레임 완료 확인",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            CompleteClaimButton.Enabled = false;
            CompleteClaimButton.Text = "완료 중...";

            var success = await _sessionService.UpdateSessionStatusAsync(SessionStatus.Completed);
            if (success)
            {
                await _sessionService.SendMessageAsync("클레임이 해결되어 완료되었습니다.", MessageType.System, "시스템");
            }

            if (success)
            {
                CompleteClaimButton.Text = "완료됨";
                CompleteClaimButton.BackColor = Color.LightGray;
                TestSessionButton.Enabled = false;

                MessageBox.Show("클레임이 성공적으로 완료되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                CompleteClaimButton.Enabled = true;
                CompleteClaimButton.Text = "클레임 완료";
                MessageBox.Show("클레임 완료 중 오류가 발생했습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void ShowSessionManager()
    {
        if (_sessionManager == null)
        {
            _sessionManager = new SessionManagerForm(_apiService);
            _sessionManager.SessionSelected += OnSessionSelected;
        }
        
        _sessionManager.ShowSessionManager();
    }

    private void HideSessionManager()
    {
        _sessionManager?.HideSessionManager();
    }

    private async void OnSessionSelected(object? sender, CustomerSession selectedSession)
    {
        if (_sessionService.CurrentSession?.SerialNumber != selectedSession.SerialNumber)
        {
            _debugLog.LogUI("직원 세션 선택", $"고객: {selectedSession.SerialNumber}");

            // 기존 세션 종료
            if (_sessionService.CurrentSession != null)
            {
                await _sessionService.EndSessionAsync("직원이 다른 세션으로 전환");
            }

            // 채팅창 클리어 (새 세션 메시지 로딩 전에)
            ChatListBox.Items.Clear();
            _debugLog.LogUI("채팅창 클리어", "새 세션 로딩 준비");
            StatusLabel.Text = "세션 연결 중...";
            StatusLabel.ForeColor = Color.Orange;

            // 선택된 세션으로 직접 전환 - 이제 CustomerSession 객체를 직접 사용
            _currentSerialNumber = selectedSession.SerialNumber;

            // 세션 객체를 직접 설정하여 서버 조회를 건너뛰기
            _sessionService.SetCurrentSession(selectedSession);

            // 메시지 히스토리 로드
            await _sessionService.LoadMessageHistoryForSession(selectedSession.CurrentSessionId);

            _debugLog.LogUI("세션 연결 성공", $"고객: {selectedSession.SerialNumber}");
            this.Text = $"Chat Supporter - {selectedSession.SerialNumber} (직원모드)";

            // 세션 상태 UI 업데이트
            CompleteClaimButton.Enabled = selectedSession.Status != SessionStatus.Completed;

            StatusLabel.Text = $"세션 연결 완료: {selectedSession.SerialNumber}";
            StatusLabel.ForeColor = Color.Green;
        }
        else
        {
            _debugLog.LogUI("동일 세션 선택", $"이미 연결된 세션: {selectedSession.SerialNumber}");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _sessionService?.Dispose();
        _deviceService?.Dispose();
        _sessionManager?.Close();
        base.OnFormClosing(e);
    }
}