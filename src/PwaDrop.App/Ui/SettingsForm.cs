using System.Diagnostics;
using PwaDrop.App.Brand;
using PwaDrop.App.Interop;
using PwaDrop.Core;

namespace PwaDrop.App.Ui;

internal sealed class SettingsForm : Form
{
    private const int TitleBarHeight = 54;
    private const int NavigationWidth = 258;
    private readonly FluentToggle _enabledToggle;
    private readonly FluentToggle _startupToggle;
    private readonly FluentToggle _notificationsToggle;
    private readonly Label _statusTitle;
    private readonly Label _statusSubtitle;
    private readonly Label _statusGlyph;
    private Button _maximizeButton = null!;
    private readonly Dictionary<NavigationButton, Control> _pages = [];
    private readonly Bitmap _brandBitmap;
    private readonly Bitmap _bridgeBitmap;
    private bool _updating;

    internal SettingsForm(AppSettings settings, string cachePath, string diagnosticsPath)
    {
        Text = "PWADrop";
        AccessibleName = "PWADrop settings";
        Icon = BrandIcon.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(900, 640);
        Size = new Size(1034, 782);
        BackColor = FluentTheme.Canvas;
        ForeColor = FluentTheme.TextPrimary;
        Font = FluentTheme.Text(10f);
        KeyPreview = true;
        _brandBitmap = BrandIcon.CreateBitmap(96);
        _bridgeBitmap = BrandIcon.CreateBridgeHeroBitmap();

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentTheme.Canvas
        };
        var titleBar = CreateTitleBar();
        var navigation = CreateNavigation();
        var contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentTheme.Canvas
        };

        Controls.Add(body);
        Controls.Add(titleBar);
        body.Controls.Add(contentHost);
        body.Controls.Add(navigation);

        var overview = CreateOverviewPage(out _enabledToggle, out _startupToggle, out _notificationsToggle, out _statusTitle, out _statusSubtitle, out _statusGlyph);
        var compatibility = CreateCompatibilityPage();
        var diagnostics = CreateDiagnosticsPage(cachePath, diagnosticsPath);
        var about = CreateAboutPage();

        contentHost.Controls.Add(overview);
        contentHost.Controls.Add(compatibility);
        contentHost.Controls.Add(diagnostics);
        contentHost.Controls.Add(about);

        var navButtons = navigation.Controls.OfType<NavigationButton>().OrderBy(button => button.Top).ToArray();
        _pages[navButtons[0]] = overview;
        _pages[navButtons[1]] = compatibility;
        _pages[navButtons[2]] = diagnostics;
        _pages[navButtons[3]] = about;
        foreach (var button in navButtons)
        {
            button.Click += (_, _) => SelectPage(button);
        }

        SelectPage(navButtons[0]);

        _enabledToggle.CheckedChanged += ToggleChanged;
        _startupToggle.CheckedChanged += ToggleChanged;
        _notificationsToggle.CheckedChanged += ToggleChanged;
        FormClosing += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Hide();
        };
        Resize += (_, _) =>
        {
            _maximizeButton.Text = WindowState == FormWindowState.Maximized ? "\uE923" : "\uE922";
            Padding = WindowState == FormWindowState.Maximized ? new Padding(7) : Padding.Empty;
        };

        ApplySettings(settings);
    }

    internal event Action<AppSettings>? SettingsChanged;

    internal void ApplySettings(AppSettings settings)
    {
        _updating = true;
        _enabledToggle.Checked = settings.Enabled;
        _startupToggle.Checked = settings.StartWithWindows;
        _notificationsToggle.Checked = settings.ShowStatusNotifications;
        _updating = false;
        SetStatus(settings.Enabled ? "Bridge active" : "Bridge paused");
    }

    internal void SetStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(status));
            return;
        }

        var active = status.Equals("Bridge active", StringComparison.OrdinalIgnoreCase);
        var paused = status.Equals("Bridge paused", StringComparison.OrdinalIgnoreCase);
        _statusTitle.Text = status;
        _statusSubtitle.Text = active
            ? "Drag priming is ready."
            : paused
                ? "Enable the bridge when you are ready."
                : "PWADrop is handling the current drag.";
        _statusGlyph.Text = active ? "\uE930" : paused ? "\uE769" : "\uE895";
        _statusGlyph.ForeColor = active ? FluentTheme.Success : paused ? FluentTheme.Warning : FluentTheme.Accent;
    }

    internal void RenderTo(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Size = new Size(1034, 782);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Show();
        Application.DoEvents();
        PerformLayout();
        Refresh();
        foreach (Control child in Controls)
        {
            child.CreateControl();
            child.PerformLayout();
            child.Refresh();
        }

        using var bitmap = new Bitmap(Width, Height);
        DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _brandBitmap.Dispose();
            _bridgeBitmap.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnHandleCreated(EventArgs eventArgs)
    {
        base.OnHandleCreated(eventArgs);
        var enabled = 1;
        var corner = 2;
        var backdrop = 2;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmwaWindowCornerPreference, ref corner, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmwaSystemBackdropType, ref backdrop, sizeof(int));
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Hide();
            return true;
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WmNcHitTest)
        {
            base.WndProc(ref message);
            if ((int)message.Result == NativeMethods.HtClient)
            {
                var screenPoint = new Point(
                    unchecked((short)((long)message.LParam & 0xFFFF)),
                    unchecked((short)(((long)message.LParam >> 16) & 0xFFFF)));
                var point = PointToClient(screenPoint);
                message.Result = (IntPtr)HitTest(point);
            }

            return;
        }

        base.WndProc(ref message);
    }

    private Panel CreateTitleBar()
    {
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = TitleBarHeight,
            BackColor = FluentTheme.Navigation
        };

        var logo = new PictureBox
        {
            Image = BrandIcon.CreateBitmap(34),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(20, 10),
            Size = new Size(34, 34),
            AccessibleName = "PWADrop logo",
            TabStop = false
        };
        var productName = new Label
        {
            Text = "PWADrop",
            AutoSize = true,
            Font = FluentTheme.Text(13.5f),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(66, 17)
        };

        var closeButton = CreateWindowButton("\uE8BB", "Close", (_, _) => Hide(), true);
        _maximizeButton = CreateWindowButton("\uE922", "Maximize or restore", (_, _) => ToggleMaximize());
        var minimizeButton = CreateWindowButton("\uE921", "Minimize", (_, _) => WindowState = FormWindowState.Minimized);
        closeButton.Dock = DockStyle.Right;
        _maximizeButton.Dock = DockStyle.Right;
        minimizeButton.Dock = DockStyle.Right;

        titleBar.Controls.Add(minimizeButton);
        titleBar.Controls.Add(_maximizeButton);
        titleBar.Controls.Add(closeButton);
        titleBar.Controls.Add(productName);
        titleBar.Controls.Add(logo);
        return titleBar;
    }

    private static Panel CreateNavigation()
    {
        var navigation = new Panel
        {
            Dock = DockStyle.Left,
            Width = NavigationWidth,
            BackColor = FluentTheme.Navigation,
            Padding = new Padding(14, 28, 14, 22)
        };

        var overview = CreateNavigationButton("Overview", "\uE80F", 35);
        var compatibility = CreateNavigationButton("Compatibility", "\uEA86", 99);
        var diagnostics = CreateNavigationButton("Diagnostics", "\uE95E", 163);
        var separator = new Panel
        {
            Location = new Point(24, 231),
            Size = new Size(NavigationWidth - 48, 1),
            BackColor = FluentTheme.Stroke,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        var about = CreateNavigationButton("About", "\uE946", 251);

        navigation.Controls.Add(overview);
        navigation.Controls.Add(compatibility);
        navigation.Controls.Add(diagnostics);
        navigation.Controls.Add(separator);
        navigation.Controls.Add(about);
        return navigation;
    }

    private Control CreateOverviewPage(
        out FluentToggle enabledToggle,
        out FluentToggle startupToggle,
        out FluentToggle notificationsToggle,
        out Label statusTitle,
        out Label statusSubtitle,
        out Label statusGlyph)
    {
        var page = CreatePage();
        page.Padding = new Padding(30, 0, 64, 0);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 280));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(layout);

        var hero = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var brand = new PictureBox
        {
            Image = _bridgeBitmap,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(280, 116),
            Anchor = AnchorStyles.None,
            AccessibleName = "PWADrop bridge mark"
        };
        var heroStatusGlyph = new Label
        {
            Text = "\uE930",
            Font = FluentTheme.Symbols(30f),
            ForeColor = FluentTheme.Success,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            AccessibleName = "Bridge status"
        };
        var heroStatusTitle = new Label
        {
            Text = "Bridge active",
            Font = FluentTheme.Display(31f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize = true,
            Anchor = AnchorStyles.None
        };
        var heroStatusSubtitle = new Label
        {
            Text = "Drag priming is ready.",
            Font = FluentTheme.Text(14.5f),
            ForeColor = FluentTheme.TextSecondary,
            AutoSize = true,
            Anchor = AnchorStyles.None
        };

        hero.Controls.Add(brand);
        hero.Controls.Add(heroStatusGlyph);
        hero.Controls.Add(heroStatusTitle);
        hero.Controls.Add(heroStatusSubtitle);
        hero.Resize += (_, _) => LayoutHero(hero, brand, heroStatusGlyph, heroStatusTitle, heroStatusSubtitle);
        layout.Controls.Add(hero, 0, 0);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = FluentTheme.Stroke }, 0, 1);

        var enabledRow = CreateSettingRow(
            "Enable drag bridge",
            "Prepare delayed files for reliable dropping into apps.",
            null,
            out enabledToggle);
        var startupRow = CreateSettingRow(
            "Start with Windows",
            "Launch PWADrop automatically when you sign in.",
            "\uE7E8",
            out startupToggle);
        var notificationsRow = CreateSettingRow(
            "Show status notifications",
            "Get notified when the bridge completes a drop or has an issue.",
            "\uEA8F",
            out notificationsToggle,
            drawBottomBorder: false);
        layout.Controls.Add(enabledRow, 0, 2);
        layout.Controls.Add(startupRow, 0, 3);
        layout.Controls.Add(notificationsRow, 0, 4);
        statusGlyph = heroStatusGlyph;
        statusTitle = heroStatusTitle;
        statusSubtitle = heroStatusSubtitle;
        return page;
    }

    private static Control CreateCompatibilityPage()
    {
        var page = CreatePage();
        var header = CreatePageHeader(
            "Compatibility",
            "PWADrop bridges delayed file drags from supported Chromium and WebView2 apps into ordinary Windows drop targets.");
        page.Controls.Add(header);

        var cards = new TableLayoutPanel
        {
            Location = new Point(44, 142),
            Size = new Size(680, 392),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334f));
        cards.Controls.Add(CreateCompatibilityCard("\uE774", "Chromium and WebView2 sources", "Edge, Chrome, installed browser apps, and recognized WebView2 hosts."), 0, 0);
        cards.Controls.Add(CreateCompatibilityCard("\uE8A5", "Browser destinations", "Standard HTML file-upload and drag-and-drop surfaces in current browsers."), 0, 1);
        cards.Controls.Add(CreateCompatibilityCard("\uE943", ".NET and Windows destinations", "WinForms, WPF, File Explorer, and other applications that accept normal file paths."), 0, 2);
        page.Controls.Add(cards);
        return page;
    }

    private static Control CreateDiagnosticsPage(string cachePath, string diagnosticsPath)
    {
        var page = CreatePage();
        page.Controls.Add(CreatePageHeader(
            "Diagnostics",
            "Inspect local bridge activity and temporary compatibility files without leaving PWADrop running in the foreground."));

        var diagnosticsCard = CreateActionCard(
            "\uE9D9",
            "Diagnostic log",
            "Open the redacted local event log used for troubleshooting.",
            "Open diagnostics",
            (_, _) => OpenPath(diagnosticsPath, createFile: true));
        diagnosticsCard.Location = new Point(44, 150);
        diagnosticsCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        diagnosticsCard.Width = 680;

        var cacheCard = CreateActionCard(
            "\uE8B7",
            "Compatibility cache",
            "Open temporary files created only for legacy virtual-file sources.",
            "Open cache",
            (_, _) => OpenPath(cachePath, createFile: false));
        cacheCard.Location = new Point(44, 300);
        cacheCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        cacheCard.Width = 680;

        page.Controls.Add(diagnosticsCard);
        page.Controls.Add(cacheCard);
        page.Resize += (_, _) =>
        {
            diagnosticsCard.Width = Math.Max(520, page.ClientSize.Width - 88);
            cacheCard.Width = Math.Max(520, page.ClientSize.Width - 88);
        };
        return page;
    }

    private Control CreateAboutPage()
    {
        var page = CreatePage();
        var logo = new PictureBox
        {
            Image = _brandBitmap,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(44, 54),
            Size = new Size(88, 88),
            AccessibleName = "PWADrop logo"
        };
        var title = new Label
        {
            Text = "PWADrop",
            Font = FluentTheme.Display(26f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(154, 60),
            AutoSize = true
        };
        var version = new Label
        {
            Text = $"Version {Application.ProductVersion}",
            Font = FluentTheme.Text(10.5f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(158, 108),
            AutoSize = true
        };
        var description = new Label
        {
            Text = "An open-source Windows bridge for dragging delayed files between modern apps.",
            Font = FluentTheme.Text(11f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(48, 176),
            Size = new Size(660, 52),
            AutoEllipsis = true
        };
        var license = new Label
        {
            Text = "Licensed under the MIT License.",
            Font = FluentTheme.Text(10.5f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(48, 242),
            AutoSize = true
        };
        var projectButton = CreateActionButton("Open project on GitHub", (_, _) =>
            Process.Start(new ProcessStartInfo("https://github.com/LowkeyNEXT/PwaDrop") { UseShellExecute = true }));
        projectButton.Location = new Point(48, 292);

        page.Controls.Add(logo);
        page.Controls.Add(title);
        page.Controls.Add(version);
        page.Controls.Add(description);
        page.Controls.Add(license);
        page.Controls.Add(projectButton);
        return page;
    }

    private static Panel CreatePage()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentTheme.Canvas,
            Visible = false
        };
    }

    private static Control CreatePageHeader(string title, string subtitle)
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 126,
            Padding = new Padding(44, 32, 44, 18),
            BackColor = Color.Transparent
        };
        var titleLabel = new Label
        {
            Text = title,
            Font = FluentTheme.Display(24f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(44, 30),
            AutoSize = true
        };
        var subtitleLabel = new Label
        {
            Text = subtitle,
            Font = FluentTheme.Text(10.5f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(47, 76),
            Size = new Size(650, 44),
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        header.Controls.Add(titleLabel);
        header.Controls.Add(subtitleLabel);
        return header;
    }

    private static FluentCard CreateCompatibilityCard(string glyph, string title, string description)
    {
        var card = new FluentCard
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(24, 18, 24, 16)
        };
        var icon = new Label
        {
            Text = glyph,
            Font = FluentTheme.Symbols(20f),
            ForeColor = FluentTheme.Accent,
            Location = new Point(24, 28),
            AutoSize = true,
            AccessibleName = title
        };
        var titleLabel = new Label
        {
            Text = title,
            Font = FluentTheme.Text(11f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(76, 20),
            AutoSize = true
        };
        var descriptionLabel = new Label
        {
            Text = description,
            Font = FluentTheme.Text(9.8f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(78, 52),
            Size = new Size(560, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        card.Controls.Add(icon);
        card.Controls.Add(titleLabel);
        card.Controls.Add(descriptionLabel);
        return card;
    }

    private static FluentCard CreateActionCard(
        string glyph,
        string title,
        string description,
        string action,
        EventHandler click)
    {
        var card = new FluentCard
        {
            Height = 126,
            Padding = new Padding(24)
        };
        var icon = new Label
        {
            Text = glyph,
            Font = FluentTheme.Symbols(20f),
            ForeColor = FluentTheme.Accent,
            Location = new Point(24, 42),
            AutoSize = true,
            AccessibleName = title
        };
        var titleLabel = new Label
        {
            Text = title,
            Font = FluentTheme.Text(11f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(76, 28),
            AutoSize = true
        };
        var descriptionLabel = new Label
        {
            Text = description,
            Font = FluentTheme.Text(9.8f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(78, 60),
            Size = new Size(380, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        var button = CreateActionButton(action, click);
        button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        button.Location = new Point(card.Width - button.Width - 24, 43);
        card.Resize += (_, _) => button.Left = card.ClientSize.Width - button.Width - 24;
        card.Controls.Add(icon);
        card.Controls.Add(titleLabel);
        card.Controls.Add(descriptionLabel);
        card.Controls.Add(button);
        return card;
    }

    private static Panel CreateSettingRow(
        string title,
        string description,
        string? glyph,
        out FluentToggle toggle,
        bool drawBottomBorder = true)
    {
        var row = new SettingRow(drawBottomBorder)
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            AccessibleName = title
        };
        var hasGlyph = !string.IsNullOrEmpty(glyph);
        var textLeft = hasGlyph ? 100 : 34;
        var titleTop = hasGlyph ? 34 : 25;
        if (!string.IsNullOrEmpty(glyph))
        {
            var icon = new Label
            {
                Text = glyph,
                Font = FluentTheme.Symbols(27f),
                ForeColor = FluentTheme.AccentSecondary,
                Location = new Point(34, 31),
                AutoSize = true,
                AccessibleName = title
            };
            row.Controls.Add(icon);
        }

        var titleLabel = new Label
        {
            Text = title,
            Font = FluentTheme.Text(13.5f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(textLeft, titleTop),
            AutoSize = true
        };
        var descriptionLabel = new Label
        {
            Text = description,
            Font = FluentTheme.Text(12f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(textLeft + 2, titleTop + 30),
            Size = new Size(520, 32),
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        var rowToggle = new FluentToggle
        {
            Location = new Point(row.Width - 90, 31),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AccessibleName = title,
            AccessibleDescription = description
        };
        row.Resize += (_, _) =>
        {
            rowToggle.Left = row.ClientSize.Width - rowToggle.Width - 4;
            descriptionLabel.Width = Math.Max(240, rowToggle.Left - descriptionLabel.Left - 24);
        };
        row.Click += (_, _) => rowToggle.Checked = !rowToggle.Checked;
        titleLabel.Click += (_, _) => rowToggle.Checked = !rowToggle.Checked;
        descriptionLabel.Click += (_, _) => rowToggle.Checked = !rowToggle.Checked;

        row.Controls.Add(titleLabel);
        row.Controls.Add(descriptionLabel);
        row.Controls.Add(rowToggle);
        toggle = rowToggle;
        return row;
    }

    private static NavigationButton CreateNavigationButton(string text, string glyph, int top)
    {
        return new NavigationButton(text, glyph)
        {
            Location = new Point(14, top),
            Width = NavigationWidth - 28,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
    }

    private static Button CreateWindowButton(string glyph, string accessibleName, EventHandler click, bool isClose = false)
    {
        var button = new Button
        {
            Text = glyph,
            AccessibleName = accessibleName,
            AccessibleRole = AccessibleRole.PushButton,
            Font = FluentTheme.Symbols(10f),
            Size = new Size(46, TitleBarHeight),
            FlatStyle = FlatStyle.Flat,
            BackColor = FluentTheme.Navigation,
            ForeColor = FluentTheme.TextPrimary,
            TabStop = true,
            Cursor = Cursors.Default
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = isClose ? Color.FromArgb(196, 43, 54) : FluentTheme.SurfaceHover;
        button.FlatAppearance.MouseDownBackColor = isClose ? Color.FromArgb(151, 32, 42) : FluentTheme.SurfacePressed;
        button.Click += click;
        return button;
    }

    private static Button CreateActionButton(string text, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(152, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = FluentTheme.Accent,
            ForeColor = Color.White,
            Font = FluentTheme.Text(9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            AccessibleName = text
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(92, 121, 255);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(58, 87, 222);
        button.Click += click;
        return button;
    }

    private static void LayoutHero(Panel hero, Control brand, Control glyph, Control title, Control subtitle)
    {
        brand.Left = (hero.ClientSize.Width - brand.Width) / 2;
        brand.Top = Math.Max(10, (hero.ClientSize.Height - 238) / 2);
        var statusWidth = glyph.Width + 12 + title.Width;
        glyph.Left = (hero.ClientSize.Width - statusWidth) / 2;
        glyph.Top = brand.Bottom + 18;
        title.Left = glyph.Right + 12;
        title.Top = glyph.Top - 6;
        subtitle.Left = (hero.ClientSize.Width - subtitle.Width) / 2;
        subtitle.Top = title.Bottom + 8;
    }

    private void SelectPage(NavigationButton selected)
    {
        foreach (var pair in _pages)
        {
            pair.Key.Selected = ReferenceEquals(pair.Key, selected);
            pair.Value.Visible = ReferenceEquals(pair.Key, selected);
        }

        _pages[selected].BringToFront();
    }

    private void ToggleChanged(object? sender, EventArgs eventArgs)
    {
        if (_updating)
        {
            return;
        }

        SettingsChanged?.Invoke(new AppSettings(
            _enabledToggle.Checked,
            _startupToggle.Checked,
            _notificationsToggle.Checked));
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private int HitTest(Point point)
    {
        const int resizeBorder = 8;
        var left = point.X < resizeBorder;
        var right = point.X >= ClientSize.Width - resizeBorder;
        var top = point.Y < resizeBorder;
        var bottom = point.Y >= ClientSize.Height - resizeBorder;

        if (top && left) return NativeMethods.HtTopLeft;
        if (top && right) return NativeMethods.HtTopRight;
        if (bottom && left) return NativeMethods.HtBottomLeft;
        if (bottom && right) return NativeMethods.HtBottomRight;
        if (left) return NativeMethods.HtLeft;
        if (right) return NativeMethods.HtRight;
        if (top) return NativeMethods.HtTop;
        if (bottom) return NativeMethods.HtBottom;
        if (point.Y < TitleBarHeight && point.X < ClientSize.Width - 138) return NativeMethods.HtCaption;
        return NativeMethods.HtClient;
    }

    private static void OpenPath(string path, bool createFile)
    {
        var directory = createFile ? Path.GetDirectoryName(path) : path;
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (createFile && !File.Exists(path))
        {
            File.WriteAllText(path, string.Empty);
        }

        Process.Start(new ProcessStartInfo(createFile ? "notepad.exe" : "explorer.exe", path) { UseShellExecute = true });
    }

    private sealed class SettingRow(bool drawBottomBorder) : Panel
    {
        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            if (!drawBottomBorder)
            {
                return;
            }

            using var pen = new Pen(FluentTheme.Stroke, 1f);
            eventArgs.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}
