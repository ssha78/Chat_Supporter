using ChatSupporter.Models;
using ChatSupporter.Services;

namespace ChatSupporter.Forms;

public partial class SessionManagerForm : Form
{
    private readonly GoogleAppsScriptService _apiService;
    private List<ChatSession> _activeSessions = new();
    private System.Windows.Forms.Timer? _refreshTimer;

    public event EventHandler<ChatSession>? SessionSelected;

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
        _refreshTimer.Interval = 10000; // 10초마다 갱신
        _refreshTimer.Tick += async (s, e) => await LoadSessionsAsync();
        _refreshTimer.Start();
    }

    public async Task LoadSessionsAsync()
    {
        try
        {
            SessionStatusLabel.Text = "세션 로딩 중...";
            SessionStatusLabel.ForeColor = Color.Blue;

            var response = await _apiService.GetActiveSessionsAsync();
            if (response.Success && response.Data != null)
            {
                _activeSessions = response.Data;
                UpdateSessionList();
                
                SessionStatusLabel.Text = $"활성 세션: {_activeSessions.Count}개";
                SessionStatusLabel.ForeColor = Color.Green;
            }
            else
            {
                SessionStatusLabel.Text = "세션 로딩 실패";
                SessionStatusLabel.ForeColor = Color.Red;
            }
        }
        catch (Exception ex)
        {
            SessionStatusLabel.Text = $"오류: {ex.Message}";
            SessionStatusLabel.ForeColor = Color.Red;
        }
    }

    private void UpdateSessionList()
    {
        SessionListView.Items.Clear();

        foreach (var session in _activeSessions)
        {
            var displayName = string.IsNullOrEmpty(session.Customer?.SerialNumber)
                ? $"고객 {session.Id[..Math.Min(8, session.Id.Length)]}"
                : session.Customer.SerialNumber;

            var item = new ListViewItem(displayName);
            
            var statusText = session.Status switch
            {
                SessionStatus.Online => "온라인",
                SessionStatus.Waiting => "🔥 직원요청",
                SessionStatus.Active => "활성",
                SessionStatus.Completed => "완료",
                _ => session.Status.ToString()
            };
            
            item.SubItems.Add(statusText);
            item.SubItems.Add(session.StartedAt.ToLocalTime().ToString("HH:mm"));
            item.SubItems.Add(session.Messages.Count.ToString());
            item.SubItems.Add(session.AssignedStaff ?? "미할당");
            item.Tag = session;

            switch (session.Status)
            {
                case SessionStatus.Online:
                    item.BackColor = Color.LightBlue;
                    break;
                case SessionStatus.Waiting:
                    item.BackColor = Color.FromArgb(255, 193, 7);
                    item.ForeColor = Color.Black;
                    item.Font = new Font(item.Font, FontStyle.Bold);
                    break;
                case SessionStatus.Active:
                    item.BackColor = Color.LightGreen;
                    break;
                case SessionStatus.Completed:
                    item.BackColor = Color.FromArgb(220, 220, 220);
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

    private void SessionListView_DoubleClick(object? sender, EventArgs e)
    {
        if (SessionListView.SelectedItems.Count > 0)
        {
            var session = SessionListView.SelectedItems[0].Tag as ChatSession;
            if (session != null)
            {
                SessionSelected?.Invoke(this, session);
            }
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
}