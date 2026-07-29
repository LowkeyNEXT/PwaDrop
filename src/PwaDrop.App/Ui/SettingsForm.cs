using System.Diagnostics;
using PwaDrop.App.Brand;
using PwaDrop.App.Interop;
using PwaDrop.Core;

namespace PwaDrop.App.Ui;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _enabledToggle;
    private readonly CheckBox _startupToggle;
    private readonly Label _status;
    private readonly Label _cachePath;
    private bool _updating;

    internal SettingsForm(AppSettings settings, string cachePath)
    {
        Text = "PwaDrop";
        Icon = BrandIcon.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(600, 500);
        Size = new Size(680, 560);
        BackColor = Color.FromArgb(247, 248, 252);
        ForeColor = Color.FromArgb(24, 29, 43);
        Font = new Font("Segoe UI Variable Text", 10f);

        var header = new BrandHeader { Dock = DockStyle.Top, Height = 132 };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(32, 24, 32, 24),
            ColumnCount = 1,
            RowCount = 4,
            BackColor = BackColor
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        Controls.Add(content);
        Controls.Add(header);
        header.BringToFront();

        _enabledToggle = CreateToggle("Enable drag bridge", "Convert New Outlook virtual files into normal Windows file drops.");
        _startupToggle = CreateToggle("Start with Windows", "Keep PwaDrop ready in the notification area after sign-in.");
        content.Controls.Add(WrapCard(_enabledToggle), 0, 0);
        content.Controls.Add(WrapCard(_startupToggle), 0, 1);

        var privacyCard = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20, 16, 20, 12), Margin = new Padding(0, 8, 0, 8) };
        var privacyTitle = new Label
        {
            Text = "Local and private",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(20, 16)
        };
        var privacyBody = new Label
        {
            Text = "Files are streamed through New Outlook's existing session, marked as Internet content, and removed after the destination has time to read them. No telemetry or mailbox login.",
            AutoEllipsis = true,
            Location = new Point(20, 44),
            Size = new Size(560, 52),
            ForeColor = Color.FromArgb(91, 98, 115),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _cachePath = new Label
        {
            Text = cachePath,
            AutoEllipsis = true,
            Location = new Point(20, 104),
            Size = new Size(560, 24),
            ForeColor = Color.FromArgb(91, 98, 115),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        var openCache = CreateButton("Open cache", (_, _) => OpenCache(cachePath));
        openCache.Location = new Point(20, 140);
        privacyCard.Controls.Add(privacyTitle);
        privacyCard.Controls.Add(privacyBody);
        privacyCard.Controls.Add(_cachePath);
        privacyCard.Controls.Add(openCache);
        content.Controls.Add(privacyCard, 0, 2);

        var footer = new Panel { Dock = DockStyle.Fill };
        _status = new Label
        {
            Text = "Ready",
            AutoSize = true,
            ForeColor = Color.FromArgb(72, 82, 255),
            Location = new Point(0, 17),
            Font = new Font(Font, FontStyle.Bold)
        };
        var close = CreateButton("Done", (_, _) => Hide());
        close.Dock = DockStyle.Right;
        footer.Controls.Add(_status);
        footer.Controls.Add(close);
        content.Controls.Add(footer, 0, 3);

        _enabledToggle.CheckedChanged += ToggleChanged;
        _startupToggle.CheckedChanged += ToggleChanged;
        FormClosing += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Hide();
        };

        ApplySettings(settings);
    }

    internal event Action<AppSettings>? SettingsChanged;

    internal void ApplySettings(AppSettings settings)
    {
        _updating = true;
        _enabledToggle.Checked = settings.Enabled;
        _startupToggle.Checked = settings.StartWithWindows;
        _updating = false;
        SetStatus(settings.Enabled ? "Bridge active" : "Bridge paused");
    }

    internal void SetStatus(string status)
    {
        _status.Text = status;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var darkMode = 0;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
    }

    private void ToggleChanged(object? sender, EventArgs eventArgs)
    {
        if (_updating)
        {
            return;
        }

        SettingsChanged?.Invoke(new AppSettings(_enabledToggle.Checked, _startupToggle.Checked));
    }

    private static CheckBox CreateToggle(string title, string description) => new()
    {
        Text = title + Environment.NewLine + description,
        CheckAlign = ContentAlignment.MiddleRight,
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Fill,
        FlatStyle = FlatStyle.Flat,
        Appearance = Appearance.Button,
        Padding = new Padding(20, 10, 18, 10),
        AutoSize = false,
        BackColor = Color.White,
        ForeColor = Color.FromArgb(24, 29, 43),
        Font = new Font("Segoe UI Variable Text", 10.5f),
        Cursor = Cursors.Hand
    };

    private static Panel WrapCard(Control content)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 0, 12) };
        panel.Controls.Add(content);
        return panel;
    }

    private static Button CreateButton(string text, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(120, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(72, 82, 255),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private static void OpenCache(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private sealed class BrandHeader : Control
    {
        internal BrandHeader()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(62, 96, 244),
                Color.FromArgb(131, 75, 236),
                15f);
            e.Graphics.FillRectangle(brush, ClientRectangle);
            using var logo = BrandIcon.CreateBitmap(76);
            e.Graphics.DrawImage(logo, new Rectangle(32, 28, 76, 76));
            using var titleFont = new Font("Segoe UI Variable Display", 24f, FontStyle.Bold);
            using var subtitleFont = new Font("Segoe UI Variable Text", 10.5f);
            using var textBrush = new SolidBrush(Color.White);
            using var mutedBrush = new SolidBrush(Color.FromArgb(220, 235, 245, 255));
            e.Graphics.DrawString("PwaDrop", titleFont, textBrush, 126, 32);
            e.Graphics.DrawString("Drag from New Outlook. Drop anywhere.", subtitleFont, mutedBrush, 130, 78);
        }
    }
}
