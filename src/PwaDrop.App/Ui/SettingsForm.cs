using System.Diagnostics;
using System.Reflection;
using PwaDrop.App.Brand;
using PwaDrop.App.Interop;
using PwaDrop.Core;

namespace PwaDrop.App.Ui;

internal sealed class SettingsForm : Form
{
    private const string ProductSite = "https://lowkeynext.github.io/PwaDrop/";
    private readonly FluentToggle _enabledToggle;
    private readonly FluentToggle _startupToggle;
    private readonly FluentToggle _notificationsToggle;
    private readonly Label _statusTitle;
    private readonly Label _statusSubtitle;
    private readonly Label _statusGlyph;
    private readonly Dictionary<FluentTabButton, Control> _pages = [];
    private readonly Bitmap _brandBitmap;
    private bool _updating;

    internal SettingsForm(AppSettings settings)
    {
        Text = "PWADrop";
        AccessibleName = "PWADrop settings";
        Icon = BrandIcon.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        MinimumSize = new Size(760, 620);
        Size = new Size(920, 760);
        BackColor = FluentTheme.Canvas;
        ForeColor = FluentTheme.TextPrimary;
        Font = FluentTheme.Text(10f);
        KeyPreview = true;
        _brandBitmap = BrandIcon.CreateBitmap(160);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentTheme.Canvas,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = CreateHeader();
        var tabs = CreateTabs(out var overviewTab, out var settingsTab);
        var contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentTheme.Canvas,
            Margin = Padding.Empty
        };

        var overview = CreateOverviewPage(out _enabledToggle, out _statusTitle, out _statusSubtitle, out _statusGlyph);
        var settingsPage = CreateSettingsPage(out _startupToggle, out _notificationsToggle);
        contentHost.Controls.Add(overview);
        contentHost.Controls.Add(settingsPage);
        _pages[overviewTab] = overview;
        _pages[settingsTab] = settingsPage;
        overviewTab.Click += (_, _) => SelectPage(overviewTab);
        settingsTab.Click += (_, _) => SelectPage(settingsTab);

        Controls.Add(root);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(contentHost, 0, 2);

        SelectPage(overviewTab);
        _enabledToggle.CheckedChanged += ToggleChanged;
        _startupToggle.CheckedChanged += ToggleChanged;
        _notificationsToggle.CheckedChanged += ToggleChanged;
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
        _notificationsToggle.Checked = settings.ShowStatusNotifications;
        _updating = false;
        SetStatus(settings.Enabled ? "Bridge active" : "Bridge paused");
    }

    internal void SetStartupManaged(bool managed)
    {
        _startupToggle.Enabled = !managed;
        _startupToggle.Cursor = managed ? Cursors.Default : Cursors.Hand;
        _startupToggle.AccessibleDescription = managed
            ? "Managed for all users by your administrator."
            : "Launch PWADrop automatically when you sign in.";
        if (_startupToggle.Tag is Label description)
        {
            description.Text = _startupToggle.AccessibleDescription;
        }
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
            ? "Ready when you drag. PWADrop can stay quietly in the notification area."
            : paused
                ? "Turn the bridge on when you want delayed file drags prepared."
                : "PWADrop is preparing the current drag.";
        _statusGlyph.Text = active ? "\uE930" : paused ? "\uE769" : "\uE895";
        _statusGlyph.ForeColor = active ? FluentTheme.Success : paused ? FluentTheme.Warning : FluentTheme.Accent;
        LayoutStatusHeader();
    }

    internal void RenderTo(string path, string pageName = "Overview", Size? renderSize = null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Size = renderSize ?? new Size(920, 760);
        SelectPage(pageName);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Show();
        Application.DoEvents();
        PrepareForRender(this);
        Application.DoEvents();

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

    private Panel CreateHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentTheme.Navigation,
            Margin = Padding.Empty
        };
        header.Controls.Add(new PictureBox
        {
            Image = _brandBitmap,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(24, 18),
            Size = new Size(64, 64),
            AccessibleName = "PWADrop logo",
            TabStop = false
        });
        header.Controls.Add(new Label
        {
            Text = "PWADrop",
            AutoSize = true,
            Font = FluentTheme.Display(20f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(104, 22)
        });
        header.Controls.Add(new Label
        {
            Text = "Drag files between modern apps",
            AutoSize = true,
            Font = FluentTheme.Text(10.5f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(106, 58)
        });
        return header;
    }

    private static Panel CreateTabs(out FluentTabButton overview, out FluentTabButton settings)
    {
        var tabs = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentTheme.Navigation,
            Margin = Padding.Empty
        };
        overview = new FluentTabButton("Overview", "\uE80F")
        {
            Location = new Point(24, 2),
            Size = new Size(138, 52)
        };
        settings = new FluentTabButton("Settings", "\uE713")
        {
            Location = new Point(168, 2),
            Size = new Size(132, 52)
        };
        tabs.Controls.Add(overview);
        tabs.Controls.Add(settings);
        return tabs;
    }

    private Control CreateOverviewPage(
        out FluentToggle enabledToggle,
        out Label statusTitle,
        out Label statusSubtitle,
        out Label statusGlyph)
    {
        var page = CreateScrollablePage();
        var statusCard = new FluentCard { Height = 252, Margin = new Padding(0, 0, 0, 16) };
        var logo = new PictureBox
        {
            Image = _brandBitmap,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(94, 94),
            AccessibleName = "PWADrop bridge mark"
        };
        statusGlyph = new Label
        {
            Text = "\uE930",
            Font = FluentTheme.Symbols(15f),
            ForeColor = FluentTheme.Success,
            AutoSize = true,
            AccessibleName = "Bridge status",
            Tag = statusCard
        };
        statusTitle = new Label
        {
            Text = "Bridge active",
            Font = FluentTheme.Display(24f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize = true,
            Tag = logo
        };
        statusSubtitle = new Label
        {
            Text = "Ready when you drag. PWADrop can stay quietly in the notification area.",
            Font = FluentTheme.Text(11f),
            ForeColor = FluentTheme.TextSecondary,
            AutoSize = false,
            TextAlign = ContentAlignment.TopCenter,
            Height = 46
        };
        statusCard.Controls.Add(logo);
        statusCard.Controls.Add(statusGlyph);
        statusCard.Controls.Add(statusTitle);
        statusCard.Controls.Add(statusSubtitle);
        var statusGlyphLabel = statusGlyph;
        var statusTitleLabel = statusTitle;
        var statusSubtitleLabel = statusSubtitle;
        statusCard.Resize += (_, _) => LayoutStatusCard(
            statusCard,
            logo,
            statusGlyphLabel,
            statusTitleLabel,
            statusSubtitleLabel);

        var bridgeCard = CreateToggleCard(
            "Enable drag bridge",
            "Prepare delayed files so ordinary Windows drop targets can receive them.",
            "\uE7C3",
            out enabledToggle);
        bridgeCard.Margin = new Padding(0, 0, 0, 16);

        var note = new FluentCard { Height = 92, Margin = Padding.Empty };
        var noteText = new Label
        {
            Text = "You can close this window. PWADrop keeps working from the notification area.",
            Font = FluentTheme.Text(10.5f),
            ForeColor = FluentTheme.TextSecondary,
            AutoSize = false,
            Location = new Point(64, 24),
            Height = 44,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        note.Controls.Add(new Label
        {
            Text = "\uE946",
            Font = FluentTheme.Symbols(18f),
            ForeColor = FluentTheme.Accent,
            Location = new Point(24, 29),
            AutoSize = true
        });
        note.Controls.Add(noteText);
        note.Resize += (_, _) => noteText.Width = Math.Max(220, note.ClientSize.Width - 88);

        page.Controls.Add(statusCard);
        page.Controls.Add(bridgeCard);
        page.Controls.Add(note);
        BindPageWidths(page, statusCard, bridgeCard, note);
        return page;
    }

    private Control CreateSettingsPage(out FluentToggle startupToggle, out FluentToggle notificationsToggle)
    {
        var page = CreateScrollablePage();
        var heading = CreateSectionHeading("Settings", "Choose how PWADrop behaves on this PC.");

        var general = new FluentCard { Height = 202, Margin = new Padding(0, 0, 0, 16), Padding = Padding.Empty };
        var startupRow = CreateToggleRow(
            "Start with Windows",
            "Launch PWADrop automatically when you sign in.",
            "\uE7E8",
            out startupToggle);
        var notificationsRow = CreateToggleRow(
            "Show status notifications",
            "Show a notification when the bridge needs your attention.",
            "\uEA8F",
            out notificationsToggle);
        startupRow.Dock = DockStyle.Top;
        notificationsRow.Dock = DockStyle.Bottom;
        general.Controls.Add(notificationsRow);
        general.Controls.Add(startupRow);

        var help = new FluentCard { Height = 176, Margin = new Padding(0, 0, 0, 16) };
        help.Controls.Add(CreateCardTitle("Help & information", "\uE897"));
        help.Controls.Add(CreateLink("Compatibility guide", ProductSite + "#compatibility", 64));
        help.Controls.Add(CreateLink("Frequently asked questions", ProductSite + "faq/", 96));
        help.Controls.Add(CreateLink("Privacy policy", ProductSite + "privacy/", 128));

        var about = new FluentCard { Height = 106, Margin = Padding.Empty };
        about.Controls.Add(new PictureBox
        {
            Image = _brandBitmap,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(22, 19),
            Size = new Size(66, 66),
            AccessibleName = "PWADrop logo"
        });
        about.Controls.Add(new Label
        {
            Text = "PWADrop",
            Font = FluentTheme.Text(12.5f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(104, 25),
            AutoSize = true
        });
        about.Controls.Add(new Label
        {
            Text = $"Version {GetDisplayVersion()} · Built by RiddleNEXT",
            Font = FluentTheme.Text(9.5f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(105, 54),
            AutoSize = true
        });

        page.Controls.Add(heading);
        page.Controls.Add(general);
        page.Controls.Add(help);
        page.Controls.Add(about);
        BindPageWidths(page, heading, general, help, about);
        return page;
    }

    private static FlowLayoutPanel CreateScrollablePage() => new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        BackColor = FluentTheme.Canvas,
        Padding = new Padding(28, 24, 28, 24),
        Margin = Padding.Empty
    };

    private static Panel CreateSectionHeading(string title, string subtitle)
    {
        var heading = new Panel { Height = 82, Margin = new Padding(0, 0, 0, 8), BackColor = Color.Transparent };
        heading.Controls.Add(new Label
        {
            Text = title,
            Font = FluentTheme.Display(23f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(2, 0),
            AutoSize = true
        });
        heading.Controls.Add(new Label
        {
            Text = subtitle,
            Font = FluentTheme.Text(10.5f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(3, 46),
            AutoSize = true
        });
        return heading;
    }

    private static FluentCard CreateToggleCard(string title, string description, string glyph, out FluentToggle toggle)
    {
        var card = new FluentCard { Height = 112, Padding = Padding.Empty };
        var row = CreateToggleRow(title, description, glyph, out toggle);
        row.Dock = DockStyle.Fill;
        card.Controls.Add(row);
        return card;
    }

    private static Panel CreateToggleRow(string title, string description, string glyph, out FluentToggle toggle)
    {
        var row = new Panel
        {
            Height = 100,
            BackColor = FluentTheme.Surface,
            Padding = Padding.Empty,
            Cursor = Cursors.Hand
        };
        var icon = new Label
        {
            Text = glyph,
            Font = FluentTheme.Symbols(18f),
            ForeColor = FluentTheme.Accent,
            Location = new Point(24, 37),
            AutoSize = true
        };
        var titleLabel = new Label
        {
            Text = title,
            Font = FluentTheme.Text(11.5f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(66, 23),
            AutoSize = true
        };
        var descriptionLabel = new Label
        {
            Text = description,
            Font = FluentTheme.Text(9.5f),
            ForeColor = FluentTheme.TextSecondary,
            Location = new Point(67, 53),
            AutoSize = false,
            Height = 28,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        toggle = new FluentToggle
        {
            Size = new Size(58, 32),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AccessibleName = title,
            AccessibleDescription = description,
            Tag = descriptionLabel
        };
        row.Controls.Add(icon);
        row.Controls.Add(titleLabel);
        row.Controls.Add(descriptionLabel);
        row.Controls.Add(toggle);
        var toggleControl = toggle;
        row.Resize += (_, _) =>
        {
            toggleControl.Location = new Point(Math.Max(120, row.ClientSize.Width - 82), 34);
            descriptionLabel.Width = Math.Max(120, toggleControl.Left - descriptionLabel.Left - 18);
        };

        void ToggleRow()
        {
            if (toggleControl.Enabled)
            {
                toggleControl.Checked = !toggleControl.Checked;
            }
        }

        row.Click += (_, _) => ToggleRow();
        titleLabel.Click += (_, _) => ToggleRow();
        descriptionLabel.Click += (_, _) => ToggleRow();
        icon.Click += (_, _) => ToggleRow();
        return row;
    }

    private static Panel CreateCardTitle(string title, string glyph)
    {
        var header = new Panel
        {
            Location = new Point(24, 20),
            Size = new Size(360, 34),
            BackColor = Color.Transparent
        };
        header.Controls.Add(new Label
        {
            Text = glyph,
            Font = FluentTheme.Symbols(15f),
            ForeColor = FluentTheme.Accent,
            Location = new Point(0, 4),
            AutoSize = true
        });
        header.Controls.Add(new Label
        {
            Text = title,
            Font = FluentTheme.Text(11.5f, FontStyle.Bold),
            ForeColor = FluentTheme.TextPrimary,
            Location = new Point(40, 3),
            AutoSize = true
        });
        return header;
    }

    private static LinkLabel CreateLink(string text, string url, int top)
    {
        var link = new LinkLabel
        {
            Text = text + "  →",
            Font = FluentTheme.Text(10f),
            LinkColor = Color.FromArgb(128, 174, 255),
            ActiveLinkColor = Color.White,
            VisitedLinkColor = Color.FromArgb(128, 174, 255),
            Location = new Point(25, top),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        link.LinkClicked += (_, _) => OpenUrl(url);
        return link;
    }

    private static void BindPageWidths(FlowLayoutPanel page, params Control[] controls)
    {
        void UpdateWidths()
        {
            var width = Math.Max(520, page.ClientSize.Width - page.Padding.Horizontal - 4);
            foreach (var control in controls)
            {
                control.Width = width;
            }
        }

        page.ClientSizeChanged += (_, _) => UpdateWidths();
        page.HandleCreated += (_, _) => UpdateWidths();
        UpdateWidths();
    }

    private static void LayoutStatusCard(Control card, Control logo, Control glyph, Control title, Control subtitle)
    {
        logo.Location = new Point((card.ClientSize.Width - logo.Width) / 2, 22);
        title.Location = new Point((card.ClientSize.Width - title.Width) / 2, 126);
        glyph.Location = new Point(Math.Max(20, title.Left - glyph.Width - 12), 135);
        subtitle.Width = Math.Min(560, Math.Max(260, card.ClientSize.Width - 80));
        subtitle.Location = new Point((card.ClientSize.Width - subtitle.Width) / 2, 174);
    }

    private void LayoutStatusHeader()
    {
        if (_statusGlyph.Tag is Control card && _statusTitle.Tag is Control logo)
        {
            LayoutStatusCard(card, logo, _statusGlyph, _statusTitle, _statusSubtitle);
        }
    }

    private void ToggleChanged(object? sender, EventArgs eventArgs)
    {
        if (_updating)
        {
            return;
        }

        SettingsChanged?.Invoke(new AppSettings(
            Enabled: _enabledToggle.Checked,
            StartWithWindows: _startupToggle.Checked,
            ShowStatusNotifications: _notificationsToggle.Checked));
    }

    private void SelectPage(FluentTabButton selected)
    {
        foreach (var pair in _pages)
        {
            pair.Key.Selected = pair.Key == selected;
            pair.Value.Visible = pair.Key == selected;
            if (pair.Value.Visible)
            {
                pair.Value.BringToFront();
            }
        }
    }

    private void SelectPage(string pageName)
    {
        var pair = _pages.FirstOrDefault(candidate =>
            candidate.Key.Text.Equals(pageName, StringComparison.OrdinalIgnoreCase));
        if (pair.Key is not null)
        {
            SelectPage(pair.Key);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // A locked-down device may prevent opening the default browser.
        }
    }

    private static void PrepareForRender(Control control)
    {
        control.CreateControl();
        control.PerformLayout();
        foreach (Control child in control.Controls)
        {
            PrepareForRender(child);
        }

        control.Refresh();
    }

    private static string GetDisplayVersion()
    {
        var value = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        }

        return (value ?? "Development").Split('+')[0];
    }
}
