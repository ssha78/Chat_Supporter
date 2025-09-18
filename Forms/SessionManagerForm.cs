using ChatSupporter.Models;
using ChatSupporter.Services;

namespace ChatSupporter.Forms;

public partial class SessionManagerForm : Form
{
    private readonly GoogleAppsScriptService _apiService;
    private List<CustomerSession> _activeSessions = new();
    private System.Windows.Forms.Timer? _refreshTimer;

    public event EventHandler<CustomerSession>? SessionSelected;

    public SessionManagerForm(GoogleAppsScriptService apiService)
    {
        _apiService = apiService;
        InitializeComponent();
        SetupForm();
        SetupRefreshTimer();
        _ = LoadSessionsAsync();
    }

    private void SetupForm()
    {
        this.Text = "세션 관리";
        this.Size = new Size(580, 720);
        this.StartPosition = FormStartPosition.Manual;
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.MinimumSize = new Size(500, 580);
        this.MaximizeBox = true;
        this.MinimizeBox = true;
        this.TopMost = true;
    }

    private void SetupRefreshTimer()
    {
        _refreshTimer = new System.Windows.Forms.Timer();
        _refreshTimer.Interval = 30000; // 30초마다 갱신 (세션 선택 방해 최소화)
        _refreshTimer.Tick += async (s, e) => await LoadSessionsAsync();
        _refreshTimer.Start();
    }

    public async Task LoadSessionsAsync()
    {
        try
        {
            // 현재 시간 정보 표시
            var koreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
            var koreaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, koreaTimeZone);

            SessionStatusLabel.Text = $"세션 로딩 중... (현재: {koreaTime:HH:mm} KST)";
            SessionStatusLabel.ForeColor = Color.Blue;

            var progress = new Progress<string>(message =>
            {
                this.Invoke(() => SessionStatusLabel.Text = message);
            });

            // 서버에서 활성 세션 조회
            var chatSessionResponse = await _apiService.GetActiveSessionsAsync(progress);
            if (chatSessionResponse.Success && chatSessionResponse.Data != null)
            {
                // ChatSession을 CustomerSession으로 변환
                var customerSessions = ConvertChatSessionsToCustomerSessions(chatSessionResponse.Data);

                // 서버에서 받은 세션에 시간 필터링 및 정렬 적용
                _activeSessions = FilterAndSortSessions(customerSessions);
                SessionStatusLabel.Text = $"활성 세션: {_activeSessions.Count}개 (서버 연결됨)";
                SessionStatusLabel.ForeColor = Color.Green;
            }
            else
            {
                // 서버 실패 시 테스트 세션 표시
                _activeSessions = CreateTestSessions();
                SessionStatusLabel.Text = $"서버 연결 실패 - 테스트 세션: {_activeSessions.Count}개 ({chatSessionResponse.Message})";
                SessionStatusLabel.ForeColor = Color.Red;
            }

            UpdateSessionList();
        }
        catch (Exception ex)
        {
            // 오류 발생 시 빈 목록
            _activeSessions = new List<CustomerSession>();
            UpdateSessionList();
            SessionStatusLabel.Text = $"오류: {ex.Message}";
            SessionStatusLabel.ForeColor = Color.Red;
        }
    }

    /// <summary>
    /// ChatSession을 CustomerSession으로 변환
    /// </summary>
    private List<CustomerSession> ConvertChatSessionsToCustomerSessions(List<ChatSession> chatSessions)
    {
        var customerSessions = new List<CustomerSession>();

        foreach (var chatSession in chatSessions)
        {
            var customerSession = new CustomerSession
            {
                SerialNumber = chatSession.Customer?.SerialNumber ?? chatSession.Id,
                DeviceModel = chatSession.Customer?.DeviceModel ?? "알 수 없음",
                CurrentSessionId = chatSession.Id,
                Status = chatSession.Status,
                IsOnline = chatSession.Status != SessionStatus.Offline && chatSession.Status != SessionStatus.Disconnected,
                LastActivity = chatSession.LastActivity,
                SessionStarted = chatSession.StartedAt,
                AssignedStaff = chatSession.AssignedStaff ?? "",
                CurrentClaimId = chatSession.CurrentClaimId ?? "",
                TotalMessages = chatSession.Messages.Count,
                LastHeartbeat = DateTime.UtcNow, // 현재 시간으로 설정
                Priority = SessionPriority.Normal,
                CreatedAt = chatSession.StartedAt,
                UpdatedAt = DateTime.UtcNow
            };

            customerSessions.Add(customerSession);
        }

        return customerSessions;
    }

    private List<CustomerSession> SortSessions(List<CustomerSession> sessions)
    {
        return sessions
            .Where(s => s.Status != SessionStatus.Completed) // 완료된 세션 제외
            .OrderByDescending(s => s.Status == SessionStatus.Waiting ? 1 : 0) // 직원 요청 세션 최우선
            .ThenByDescending(s => s.LastActivity) // 최근 활동순
            .ToList();
    }

    private List<CustomerSession> FilterAndSortSessions(List<CustomerSession> sessions)
    {
        // 임시로 1시간 필터 비활성화 - 모든 미완료 세션 표시
        var filteredSessions = sessions
            .Where(s => s.Status != SessionStatus.Completed); // 완료된 세션만 제외

        // 장비별로 그룹화하여 최신 세션만 표시
        var groupedSessions = filteredSessions
            .GroupBy(s => s.SerialNumber)
            .Select(group => {
                // 각 장비의 세션들을 최근 활동순으로 정렬하여 가장 최신 세션을 선택
                var latestSession = group
                    .OrderByDescending(s => s.Status == SessionStatus.Waiting ? 1 : 0) // 직원 요청 세션 최우선
                    .ThenByDescending(s => s.LastActivity) // 최근 활동순
                    .First();

                // 해당 장비의 총 세션 수와 총 메시지 수 계산
                var totalSessions = group.Count();
                var totalMessages = group.Sum(s => s.TotalMessages);

                // 복사본 생성하여 수정 (원본 객체 변경 방지)
                var groupedSession = new CustomerSession
                {
                    SerialNumber = latestSession.SerialNumber,
                    DeviceModel = totalSessions > 1 ? $"{latestSession.DeviceModel} ({totalSessions}개 세션)" : latestSession.DeviceModel,
                    CurrentSessionId = latestSession.CurrentSessionId,
                    Status = latestSession.Status,
                    IsOnline = latestSession.IsOnline,
                    LastActivity = latestSession.LastActivity,
                    SessionStarted = latestSession.SessionStarted,
                    AssignedStaff = latestSession.AssignedStaff,
                    CurrentClaimId = latestSession.CurrentClaimId,
                    TotalMessages = totalMessages, // 모든 세션의 메시지 합계
                    LastHeartbeat = latestSession.LastHeartbeat,
                    Priority = latestSession.Priority,
                    CreatedAt = latestSession.CreatedAt,
                    UpdatedAt = latestSession.UpdatedAt
                };

                return groupedSession;
            })
            .OrderByDescending(s => s.Status == SessionStatus.Waiting ? 1 : 0) // 직원 요청 세션 최우선
            .ThenByDescending(s => s.LastActivity) // 최근 활동순
            .ToList();

        // 디버그 정보 추가
        SessionStatusLabel.Text += $" (장비별 그룹화 적용)";

        return groupedSessions;
    }

    private void UpdateSessionList()
    {
        SessionListView.Items.Clear();

        foreach (var session in _activeSessions)
        {
            var displayName = string.IsNullOrEmpty(session.SerialNumber)
                ? $"고객 {session.CurrentSessionId[..Math.Min(8, session.CurrentSessionId.Length)]}"
                : session.SerialNumber;

            var item = new ListViewItem(displayName);

            var statusText = session.Status switch
            {
                SessionStatus.Offline => "오프라인",
                SessionStatus.Online => "온라인",
                SessionStatus.Waiting => "🚨 직원요청",
                SessionStatus.Active => $"✅ 상담중 ({session.AssignedStaff ?? "미지정"})",
                SessionStatus.Completed => "완료",
                SessionStatus.Disconnected => "연결 끊김",
                _ => session.Status.ToString()
            };

            // 담당 직원 표시
            var assignedStaff = string.IsNullOrEmpty(session.AssignedStaff)
                ? "미할당"
                : session.AssignedStaff;

            item.SubItems.Add(statusText);
            item.SubItems.Add(session.SessionStarted.ToLocalTime().ToString("HH:mm"));
            item.SubItems.Add(session.TotalMessages.ToString());
            item.SubItems.Add(assignedStaff); // 담당 직원
            item.Tag = session;

            switch (session.Status)
            {
                case SessionStatus.Offline:
                    item.BackColor = Color.FromArgb(220, 220, 220);
                    item.ForeColor = Color.Gray;
                    break;
                case SessionStatus.Online:
                    item.BackColor = Color.LightBlue;
                    break;
                case SessionStatus.Waiting:
                    // 직원 요청 - 빨간 배경으로 강조
                    item.BackColor = Color.FromArgb(255, 99, 71); // 토마토색
                    item.ForeColor = Color.White;
                    item.Font = new Font(item.Font, FontStyle.Bold);
                    break;
                case SessionStatus.Active:
                    // 상담 중 - 초록 배경
                    item.BackColor = Color.FromArgb(144, 238, 144); // 연한 초록
                    item.ForeColor = Color.Black;
                    break;
                case SessionStatus.Completed:
                    item.BackColor = Color.FromArgb(220, 220, 220);
                    item.ForeColor = Color.Gray;
                    break;
                case SessionStatus.Disconnected:
                    item.BackColor = Color.FromArgb(255, 182, 193); // 연한 분홍
                    item.ForeColor = Color.DarkRed;
                    break;
                default:
                    item.BackColor = Color.White;
                    break;
            }

            SessionListView.Items.Add(item);
        }
    }

    private void RefreshButton_Click(object? sender, EventArgs e)
    {
        _ = LoadSessionsAsync();
    }

    private async void SessionListView_DoubleClick(object? sender, EventArgs e)
    {
        if (SessionListView.SelectedItems.Count > 0)
        {
            var session = SessionListView.SelectedItems[0].Tag as CustomerSession;
            if (session != null)
            {
                // 서버 기반 세션 할당 처리
                await HandleSessionAssignmentAsync(session);
            }
        }
    }

    private async Task HandleSessionAssignmentAsync(CustomerSession session)
    {
        var staffId = "김직원"; // 추후 로그인 시스템으로 변경 예정

        try
        {
            // UI 즉시 업데이트 (응답성 향상)
            SessionStatusLabel.Text = $"세션 할당 중... ({session.SerialNumber})";
            SessionStatusLabel.ForeColor = Color.Orange;

            // 이미 할당된 세션인지 확인
            if (!string.IsNullOrEmpty(session.AssignedStaff))
            {
                // 이미 할당된 세션 - 바로 선택
                SessionSelected?.Invoke(this, session);
                SessionStatusLabel.Text = $"세션 연결됨: {session.SerialNumber} (기존 할당)";
                SessionStatusLabel.ForeColor = Color.Green;
                return;
            }

            // 서버에 세션 할당 요청
            var assignmentResult = await _apiService.AssignStaffToSessionAsync(
                session.CurrentSessionId,
                staffId,
                staffId
            );

            if (assignmentResult.Success && assignmentResult.Data != null)
            {
                // 할당 성공 - 로컬 세션 정보 업데이트
                session.AssignedStaff = staffId;

                // 세션 상태를 Active로 변경 (서버에서 이미 처리됨)
                if (session.Status == SessionStatus.Waiting || session.Status == SessionStatus.Online)
                {
                    session.Status = SessionStatus.Active;
                }

                // UI 업데이트
                UpdateSessionList();
                SessionSelected?.Invoke(this, session);

                SessionStatusLabel.Text = $"세션 할당 성공: {session.SerialNumber}";
                SessionStatusLabel.ForeColor = Color.Green;
            }
            else
            {
                // 할당 실패 (다른 직원이 이미 할당했거나 서버 오류)
                var message = assignmentResult.Data?.AlreadyAssigned == true
                    ? $"다른 직원({assignmentResult.Data.ConflictStaff})이 이미 담당하고 있습니다."
                    : assignmentResult.Message ?? "세션 할당에 실패했습니다.";

                MessageBox.Show(message, "세션 할당 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                SessionStatusLabel.Text = $"할당 실패: {message}";
                SessionStatusLabel.ForeColor = Color.Red;

                // 세션 목록 새로고침 (서버 상태와 동기화)
                _ = LoadSessionsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"세션 할당 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SessionStatusLabel.Text = $"오류: {ex.Message}";
            SessionStatusLabel.ForeColor = Color.Red;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
        }
        else
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }

    public void ShowSessionManager()
    {
        this.Show();
        this.BringToFront();
        _ = LoadSessionsAsync();
    }

    public void HideSessionManager()
    {
        this.Hide();
    }

    /// <summary>
    /// GAS 비활성화 시 테스트용 세션 데이터 생성
    /// </summary>
    private List<CustomerSession> CreateTestSessions()
    {
        var testSessions = new List<CustomerSession>();
        var now = DateTime.UtcNow;

        // 테스트 세션 1: 고객 대기 중
        testSessions.Add(new CustomerSession
        {
            SerialNumber = "LM1234",
            DeviceModel = "L-CAM Pro",
            CurrentSessionId = "LM1234_SESSION_001",
            Status = SessionStatus.Waiting,
            IsOnline = true,
            LastActivity = now.AddMinutes(-5),
            SessionStarted = now.AddMinutes(-10),
            AssignedStaff = "",
            CurrentClaimId = "",
            TotalMessages = 3,
            LastHeartbeat = now.AddMinutes(-1),
            Priority = SessionPriority.High,
            CreatedAt = now.AddMinutes(-10),
            UpdatedAt = now.AddMinutes(-1)
        });

        // 테스트 세션 2: 상담 중
        testSessions.Add(new CustomerSession
        {
            SerialNumber = "LM5678",
            DeviceModel = "L-CAM Standard",
            CurrentSessionId = "LM5678_SESSION_002",
            Status = SessionStatus.Active,
            IsOnline = true,
            LastActivity = now.AddMinutes(-2),
            SessionStarted = now.AddMinutes(-15),
            AssignedStaff = "김직원",
            CurrentClaimId = "",
            TotalMessages = 8,
            LastHeartbeat = now,
            Priority = SessionPriority.Normal,
            CreatedAt = now.AddMinutes(-15),
            UpdatedAt = now.AddMinutes(-2)
        });

        // 테스트 세션 3: 온라인 대기
        testSessions.Add(new CustomerSession
        {
            SerialNumber = "LM9999",
            DeviceModel = "L-CAM Test",
            CurrentSessionId = "LM9999_SESSION_003",
            Status = SessionStatus.Online,
            IsOnline = true,
            LastActivity = now.AddMinutes(-1),
            SessionStarted = now.AddMinutes(-3),
            AssignedStaff = "",
            CurrentClaimId = "",
            TotalMessages = 1,
            LastHeartbeat = now,
            Priority = SessionPriority.Normal,
            CreatedAt = now.AddMinutes(-3),
            UpdatedAt = now.AddMinutes(-1)
        });

        return testSessions;
    }
}