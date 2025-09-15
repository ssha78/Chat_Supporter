namespace ChatSupporter.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private ListBox ChatListBox = null!;
    private TextBox MessageTextBox = null!;
    private Button SendButton = null!;
    private Button ConnectToStaffButton = null!;
    private Button StaffModeButton = null!;
    private Label StatusLabel = null!;
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
        this.ChatListBox = new ListBox();
        this.MessageTextBox = new TextBox();
        this.SendButton = new Button();
        this.ConnectToStaffButton = new Button();
        this.StaffModeButton = new Button();
        this.StatusLabel = new Label();
        this.SessionStatusLabel = new Label();
        this.SuspendLayout();
        
        // ChatListBox
        this.ChatListBox.Location = new Point(12, 12);
        this.ChatListBox.Size = new Size(760, 400);
        this.ChatListBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.ChatListBox.Font = new Font("맑은 고딕", 9F);
        this.ChatListBox.ScrollAlwaysVisible = true;
        
        // MessageTextBox
        this.MessageTextBox.Location = new Point(12, 430);
        this.MessageTextBox.Size = new Size(600, 25);
        this.MessageTextBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.MessageTextBox.Font = new Font("맑은 고딕", 9F);
        this.MessageTextBox.KeyPress += MessageTextBox_KeyPress;
        
        // SendButton
        this.SendButton.Location = new Point(620, 428);
        this.SendButton.Size = new Size(75, 30);
        this.SendButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        this.SendButton.Text = "전송";
        this.SendButton.UseVisualStyleBackColor = true;
        this.SendButton.Click += SendButton_Click;
        
        // ConnectToStaffButton
        this.ConnectToStaffButton.Location = new Point(12, 470);
        this.ConnectToStaffButton.Size = new Size(120, 30);
        this.ConnectToStaffButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.ConnectToStaffButton.Text = "직원 연결 요청";
        this.ConnectToStaffButton.BackColor = Color.LightBlue;
        this.ConnectToStaffButton.UseVisualStyleBackColor = false;
        this.ConnectToStaffButton.Click += ConnectToStaffButton_Click;
        
        // StaffModeButton
        this.StaffModeButton.Location = new Point(140, 470);
        this.StaffModeButton.Size = new Size(100, 30);
        this.StaffModeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.StaffModeButton.Text = "직원 모드";
        this.StaffModeButton.UseVisualStyleBackColor = true;
        this.StaffModeButton.Click += StaffModeButton_Click;
        
        // StatusLabel
        this.StatusLabel.Location = new Point(250, 475);
        this.StatusLabel.Size = new Size(300, 20);
        this.StatusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.StatusLabel.Text = "준비 중...";
        this.StatusLabel.ForeColor = Color.Gray;
        
        // SessionStatusLabel
        this.SessionStatusLabel.Location = new Point(560, 475);
        this.SessionStatusLabel.Size = new Size(200, 20);
        this.SessionStatusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        this.SessionStatusLabel.Text = "세션 없음";
        this.SessionStatusLabel.ForeColor = Color.Gray;
        this.SessionStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        
        // MainForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(784, 511);
        this.Controls.Add(this.ChatListBox);
        this.Controls.Add(this.MessageTextBox);
        this.Controls.Add(this.SendButton);
        this.Controls.Add(this.ConnectToStaffButton);
        this.Controls.Add(this.StaffModeButton);
        this.Controls.Add(this.StatusLabel);
        this.Controls.Add(this.SessionStatusLabel);
        this.Text = "Chat Supporter";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}