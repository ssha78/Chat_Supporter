namespace ChatSupporter.Forms;

partial class SessionManagerForm
{
    private System.ComponentModel.IContainer components = null;
    private ListView SessionListView = null!;
    private Button RefreshButton = null!;
    private Label SessionStatusLabel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.SessionListView = new ListView();
        this.RefreshButton = new Button();
        this.SessionStatusLabel = new Label();
        this.SuspendLayout();
        
        // SessionListView
        this.SessionListView.Location = new Point(12, 12);
        this.SessionListView.Size = new Size(540, 600);
        this.SessionListView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.SessionListView.View = View.Details;
        this.SessionListView.FullRowSelect = true;
        this.SessionListView.GridLines = true;
        this.SessionListView.MultiSelect = false;
        
        // 컬럼 설정
        this.SessionListView.Columns.Add("고객", 120);
        this.SessionListView.Columns.Add("상태", 80);
        this.SessionListView.Columns.Add("시작시간", 80);
        this.SessionListView.Columns.Add("메시지수", 80);
        this.SessionListView.Columns.Add("담당직원", 100);
        
        this.SessionListView.DoubleClick += SessionListView_DoubleClick;
        
        // RefreshButton
        this.RefreshButton.Location = new Point(12, 625);
        this.RefreshButton.Size = new Size(75, 30);
        this.RefreshButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.RefreshButton.Text = "새로고침";
        this.RefreshButton.UseVisualStyleBackColor = true;
        this.RefreshButton.Click += RefreshButton_Click;
        
        // SessionStatusLabel
        this.SessionStatusLabel.Location = new Point(100, 632);
        this.SessionStatusLabel.Size = new Size(450, 20);
        this.SessionStatusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.SessionStatusLabel.Text = "세션 로딩 중...";
        this.SessionStatusLabel.ForeColor = Color.Gray;
        
        // SessionManagerForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(564, 671);
        this.Controls.Add(this.SessionListView);
        this.Controls.Add(this.RefreshButton);
        this.Controls.Add(this.SessionStatusLabel);
        this.Text = "세션 관리";
        this.ResumeLayout(false);
    }
}