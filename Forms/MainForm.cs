using ChatSupporter.Models;
using ChatSupporter.Services;

namespace ChatSupporter.Forms;

public partial class MainForm : Form
{
    private readonly ConfigurationService _configService;
    private readonly GoogleAppsScriptService _apiService;
    private readonly SessionService _sessionService;
    private readonly DeviceDetectionService _deviceService;
    
    private SessionManagerForm? _sessionManager;
    private bool _isStaffMode = false;
    private string _currentSerialNumber = string.Empty;

    public MainForm()
    {
        InitializeComponent();
        
        _configService = new ConfigurationService();
        _apiService = new GoogleAppsScriptService(
            _configService.Settings.GoogleAppsScript.ChatApiUrl,
            _configService.Settings.GoogleAppsScript.MaxRetries,
            _configService.Settings.GoogleAppsScript.TimeoutSeconds
        );
        _sessionService = new SessionService(_apiService, _configService);
        _deviceService = new DeviceDetectionService();
        
        SetupForm();
        SetupEvents();
        DetectDevicesAndStartSession();
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
            
            var customer = new Customer
            {
                SerialNumber = device.SerialNumber,
                DeviceModel = device.DeviceModel
            };
            
            _ = _sessionService.StartSessionAsync(_currentSerialNumber, customer);
            
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
                
                var customer = new Customer
                {
                    SerialNumber = device.SerialNumber,
                    DeviceModel = device.DeviceModel
                };
                
                _ = _sessionService.StartSessionAsync(_currentSerialNumber, customer);
                
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

    private void OnSessionStatusChanged(object? sender, ChatSession session)
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
        else if (message.IsFromStaff)
        {
            displayText += "[직원] ";
        }
        else
        {
            displayText += $"[{message.Sender}] ";
        }
        
        displayText += message.Content;
        
        ChatListBox.Items.Add(displayText);
        ChatListBox.TopIndex = ChatListBox.Items.Count - 1;
    }

    private async void SendButton_Click(object? sender, EventArgs e)
    {
        var message = MessageTextBox.Text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        MessageTextBox.Text = string.Empty;
        
        var messageType = _isStaffMode ? MessageType.Staff : MessageType.User;
        var success = await _sessionService.SendMessageAsync(message, messageType);
        
        if (!success)
        {
            MessageBox.Show("메시지 전송에 실패했습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void MessageTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)Keys.Enter)
        {
            e.Handled = true;
            SendButton_Click(sender, e);
        }
    }

    private async void ConnectToStaffButton_Click(object? sender, EventArgs e)
    {
        if (_sessionService.CurrentSession?.Status == SessionStatus.Online)
        {
            var success = await _sessionService.UpdateSessionStatusAsync(SessionStatus.Waiting);
            if (success)
            {
                await _sessionService.SendMessageAsync("고객이 직원 연결을 요청했습니다.", MessageType.System);
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
            StaffModeButton.Text = "고객 모드";
            StaffModeButton.BackColor = Color.Orange;
            ConnectToStaffButton.Visible = false;
            
            ShowSessionManager();
        }
        else
        {
            StaffModeButton.Text = "직원 모드";
            StaffModeButton.BackColor = SystemColors.Control;
            ConnectToStaffButton.Visible = true;
            
            HideSessionManager();
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

    private async void OnSessionSelected(object? sender, ChatSession session)
    {
        if (_sessionService.CurrentSession?.Id != session.Id)
        {
            _sessionService.EndSession();
            
            _currentSerialNumber = session.Customer?.SerialNumber ?? session.Id;
            await _sessionService.StartSessionAsync(_currentSerialNumber, session.Customer);
            
            ChatListBox.Items.Clear();
            this.Text = $"Chat Supporter - {session.Customer?.SerialNumber ?? "Unknown"}";
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