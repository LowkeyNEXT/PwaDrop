using System.Runtime.InteropServices;
using PwaDrop.App.Brand;
using PwaDrop.App.Diagnostics;
using PwaDrop.App.Drag;
using PwaDrop.App.Interop;
using PwaDrop.App.Ui;
using PwaDrop.Core;

namespace PwaDrop.App;

internal sealed class PwaDropApplicationContext : ApplicationContext
{
    private readonly string _dataPath;
    private readonly string _settingsPath;
    private readonly DiagnosticLog _diagnostics;
    private readonly CacheManager _cache;
    private readonly SourceHookManager _sourceHookManager;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _enabledMenuItem;
    private bool _startupManaged;
    private SettingsForm? _settingsForm;
    private AppSettings _settings;

    internal PwaDropApplicationContext()
    {
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PwaDrop");
        _settingsPath = Path.Combine(_dataPath, "settings.json");
        _diagnostics = new DiagnosticLog(Path.Combine(_dataPath, "diagnostics.log"));
        _settings = AppSettings.Load(_settingsPath);
        SynchronizeStartupState();
        _cache = new CacheManager(Path.Combine(_dataPath, "Cache"));
        _cache.PurgeExpired(DateTimeOffset.UtcNow);
        _sourceHookManager = new SourceHookManager(
            Path.Combine(AppContext.BaseDirectory, "Hook"),
            _diagnostics);

        _statusMenuItem = new ToolStripMenuItem("Bridge active")
        {
            Enabled = false,
            Font = new Font("Segoe UI Variable Text", 9.5f, FontStyle.Bold)
        };
        _enabledMenuItem = new ToolStripMenuItem("Enable drag bridge")
        {
            Checked = _settings.Enabled,
            CheckOnClick = true
        };
        _enabledMenuItem.Click += (_, _) =>
            ApplySettings(_settings with { Enabled = _enabledMenuItem.Checked });

        var menu = new ContextMenuStrip
        {
            BackColor = FluentTheme.Surface,
            ForeColor = FluentTheme.TextPrimary,
            Font = new Font("Segoe UI Variable Text", 9.5f),
            Renderer = new FluentToolStripRenderer(),
            ShowImageMargin = false,
            Padding = new Padding(4),
            MinimumSize = new Size(228, 0)
        };
        menu.Items.Add(_statusMenuItem);
        menu.Items.Add("Open PWADrop", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_enabledMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _trayIcon = new NotifyIcon
        {
            Icon = BrandIcon.CreateIcon(64),
            Text = "PWADrop — Bridge active",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        ApplySettings(_settings, persist: false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sourceHookManager.Dispose();
            _settingsForm?.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private async void ApplySettings(AppSettings settings, bool persist = true)
    {
        var startupChanged = persist && settings.StartWithWindows != _settings.StartWithWindows;
        _settings = settings;

        if (settings.Enabled && !_sourceHookManager.IsRunning)
        {
            try
            {
                _sourceHookManager.Start();
            }
            catch (Exception exception)
            {
                _settings = settings with { Enabled = false };
                ShowError("PWADrop could not start its source hook.", exception.HResult);
            }
        }
        else if (!settings.Enabled && _sourceHookManager.IsRunning)
        {
            _sourceHookManager.Stop();
        }

        _enabledMenuItem.Checked = _settings.Enabled;
        _enabledMenuItem.Text = "Enable drag bridge";
        _settingsForm?.ApplySettings(_settings);
        SetStatus(_settings.Enabled ? "Bridge active" : "Bridge paused");

        if (startupChanged)
        {
            try
            {
                var enabled = await StartupRegistration.SetEnabledAsync(_settings.StartWithWindows);
                if (enabled != _settings.StartWithWindows)
                {
                    _settings = _settings with { StartWithWindows = enabled };
                    _settingsForm?.ApplySettings(_settings);
                    ShowError("Windows did not allow PWADrop to change its startup setting.", 0);
                }

                var startupState = await StartupRegistration.GetStateAsync();
                _startupManaged = startupState.Managed;
                if (_settings.StartWithWindows != startupState.Enabled)
                {
                    _settings = _settings with { StartWithWindows = startupState.Enabled };
                    _settingsForm?.ApplySettings(_settings);
                }
                _settingsForm?.SetStartupManaged(_startupManaged);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or COMException or IOException or
                System.Security.SecurityException)
            {
                _settings = _settings with { StartWithWindows = false };
                _settingsForm?.ApplySettings(_settings);
                ShowError(
                    "Windows did not allow PWADrop to change its startup setting.",
                    exception.HResult);
            }
        }

        if (persist)
        {
            _settings.Save(_settingsPath);
        }
    }

    private void ShowSettings()
    {
        _settingsForm ??= CreateSettingsForm();
        _settingsForm.ApplySettings(_settings);
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private SettingsForm CreateSettingsForm()
    {
        var form = new SettingsForm(_settings);
        form.SetStartupManaged(_startupManaged);
        form.SettingsChanged += settings => ApplySettings(settings);
        return form;
    }

    private void SynchronizeStartupState()
    {
        try
        {
            var state = StartupRegistration.GetStateAsync().GetAwaiter().GetResult();
            _startupManaged = state.Managed;
            if (_settings.StartWithWindows != state.Enabled)
            {
                _settings = _settings with { StartWithWindows = state.Enabled };
                _settings.Save(_settingsPath);
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or COMException or IOException or
            System.Security.SecurityException)
        {
            _startupManaged = false;
        }
    }

    private void SetStatus(string status)
    {
        _settingsForm?.SetStatus(status);
        _statusMenuItem.Text = status;
        _trayIcon.Text = status.Length <= 63 ? $"PWADrop — {status}" : "PWADrop";
    }

    private void ShowError(string message, int errorCode)
    {
        _trayIcon.ShowBalloonTip(
            4000,
            "PWADrop",
            $"{message} Error 0x{errorCode:X8}.",
            ToolTipIcon.Warning);
    }

    private void Exit()
    {
        _trayIcon.Visible = false;
        ExitThread();
    }
}
