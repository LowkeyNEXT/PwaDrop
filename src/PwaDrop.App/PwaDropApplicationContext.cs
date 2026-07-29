using System.Diagnostics;
using Microsoft.Win32;
using PwaDrop.App.Brand;
using PwaDrop.App.Diagnostics;
using PwaDrop.App.Drag;
using PwaDrop.App.Interop;
using PwaDrop.App.Ui;
using PwaDrop.Core;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.App;

internal sealed class PwaDropApplicationContext : ApplicationContext
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "PwaDrop";
    private readonly string _dataPath;
    private readonly string _settingsPath;
    private readonly DiagnosticLog _diagnostics;
    private readonly CacheManager _cache;
    private readonly VirtualFileExtractor _extractor;
    private readonly RelayOverlayForm _overlay;
    private readonly OutlookDragMonitor _monitor;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _enabledMenuItem;
    private readonly Control _dispatcher;
    private readonly CancellationTokenSource _shutdown = new();
    private SettingsForm? _settingsForm;
    private AppSettings _settings;
    private bool _relayBusy;
    private DateTimeOffset _lastUnsupportedNotice;

    internal PwaDropApplicationContext()
    {
        _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PwaDrop");
        _settingsPath = Path.Combine(_dataPath, "settings.json");
        _diagnostics = new DiagnosticLog(Path.Combine(_dataPath, "diagnostics.log"));
        var cachePath = Path.Combine(_dataPath, "Cache");
        _settings = AppSettings.Load(_settingsPath);
        _cache = new CacheManager(cachePath);
        _cache.PurgeExpired(DateTimeOffset.UtcNow);
        _extractor = new VirtualFileExtractor(_cache);
        _dispatcher = new Control();
        _dispatcher.CreateControl();
        _overlay = new RelayOverlayForm(
            _extractor,
            HandleVirtualDrop,
            HandleRelayLeave,
            HandleUnsupportedDrag);
        _ = _overlay.Handle;
        _monitor = new OutlookDragMonitor(_overlay, GetExcludedWindows);

        _enabledMenuItem = new ToolStripMenuItem("Bridge enabled")
        {
            Checked = _settings.Enabled,
            CheckOnClick = true
        };
        _enabledMenuItem.Click += (_, _) => ApplySettings(_settings with { Enabled = _enabledMenuItem.Checked });

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add("Open cache", null, (_, _) => OpenCache());
        menu.Items.Add("Open diagnostics", null, (_, _) => OpenDiagnostics());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _trayIcon = new NotifyIcon
        {
            Icon = BrandIcon.CreateIcon(64),
            Text = "PwaDrop — New Outlook drag bridge",
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
            _shutdown.Cancel();
            _monitor.Dispose();
            _overlay.Dispose();
            _settingsForm?.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _dispatcher.Dispose();
            _shutdown.Dispose();
        }

        base.Dispose(disposing);
    }

    private IReadOnlyList<IntPtr> GetExcludedWindows()
    {
        var windows = new List<IntPtr> { _overlay.Handle };
        if (_settingsForm is { IsHandleCreated: true })
        {
            windows.Add(_settingsForm.Handle);
        }

        return windows;
    }

    private bool HandleVirtualDrop(
        ComTypes.IDataObject dataObject,
        NativeMethods.PointL point,
        DragPayloadKind payloadKind)
    {
        if (_relayBusy)
        {
            return false;
        }

        _relayBusy = true;
        _overlay.HideRelay();
        var operationStarted = Stopwatch.GetTimestamp();
        _diagnostics.ExtractionStarted(payloadKind);
        try
        {
            SetStatus("Downloading from Outlook…");
            var extractionTask = _extractor.ExtractAfterDropAsync(dataObject, payloadKind);
            _ = CompleteVirtualDropAsync(extractionTask, payloadKind, operationStarted);
            return true;
        }
        catch (Exception exception)
        {
            _relayBusy = false;
            SetStatus("Bridge active");
            _diagnostics.ExtractionFailed(
                payloadKind,
                exception.HResult,
                Stopwatch.GetElapsedTime(operationStarted));
            ShowError("PwaDrop could not prepare that item.", exception.HResult);
            return false;
        }
    }

    private async Task CompleteVirtualDropAsync(
        Task<ExtractionResult> extractionTask,
        DragPayloadKind payloadKind,
        long operationStarted)
    {
        try
        {
            var extraction = await extractionTask.ConfigureAwait(false);
            _diagnostics.ExtractionCompleted(
                payloadKind,
                extraction.Files.Count,
                Stopwatch.GetElapsedTime(operationStarted));
            if (!_shutdown.IsCancellationRequested)
            {
                _dispatcher.BeginInvoke(() => ReplayExtraction(extraction, operationStarted));
            }
        }
        catch (Exception exception)
        {
            if (!_shutdown.IsCancellationRequested)
            {
                _diagnostics.ExtractionFailed(
                    payloadKind,
                    exception.HResult,
                    Stopwatch.GetElapsedTime(operationStarted));
                _dispatcher.BeginInvoke(() => HandleExtractionFailure(exception.HResult));
            }
        }
    }

    private void HandleExtractionFailure(int errorCode)
    {
        _relayBusy = false;
        SetStatus(_settings.Enabled ? "Bridge active" : "Bridge paused");
        ShowError("PwaDrop could not prepare that item.", errorCode);
    }

    private void ReplayExtraction(ExtractionResult extraction, long operationStarted)
    {
        try
        {
            SetStatus("Dropping files…");
            var replay = PhysicalFileReplay.Replay(extraction.Files);
            _diagnostics.ReplayCompleted(replay, Stopwatch.GetElapsedTime(operationStarted));
            if (replay.Accepted)
            {
                _trayIcon.ShowBalloonTip(1500, "PwaDrop", $"Dropped {extraction.Files.Count} file{(extraction.Files.Count == 1 ? string.Empty : "s")}.", ToolTipIcon.Info);
            }
            else
            {
                _trayIcon.ShowBalloonTip(
                    5000,
                    "PwaDrop",
                    $"The destination declined the replay. OLE 0x{replay.HResult:X8}, effect 0x{(uint)replay.Effect:X8}.",
                    ToolTipIcon.Warning);
            }
        }
        catch (Exception exception)
        {
            _diagnostics.ReplayFailed(
                exception.HResult,
                Stopwatch.GetElapsedTime(operationStarted));
            ShowError("The destination did not accept the prepared files.", exception.HResult);
        }
        finally
        {
            _relayBusy = false;
            SetStatus(_settings.Enabled ? "Bridge active" : "Bridge paused");
            _ = _cache.DeleteSessionAfterDelayAsync(
                extraction.SessionPath,
                CacheManager.SuccessfulDropLifetime,
                _shutdown.Token);
        }
    }

    private void HandleRelayLeave()
    {
        if (!_relayBusy)
        {
            _overlay.HideRelay();
        }
    }

    private void HandleUnsupportedDrag()
    {
        _diagnostics.UnsupportedPayload();
        _dispatcher.BeginInvoke(() =>
        {
            _overlay.HideRelay();
            SetStatus(_settings.Enabled ? "Bridge active" : "Bridge paused");

            var now = DateTimeOffset.UtcNow;
            if (now - _lastUnsupportedNotice < TimeSpan.FromSeconds(10))
            {
                return;
            }

            _lastUnsupportedNotice = now;
            _trayIcon.ShowBalloonTip(
                3500,
                "PwaDrop",
                "That Outlook drag did not expose a downloadable file. Try an email row or attachment.",
                ToolTipIcon.Info);
        });
    }

    private void ApplySettings(AppSettings settings, bool persist = true)
    {
        _settings = settings;

        if (settings.Enabled && !_monitor.IsRunning)
        {
            try
            {
                _monitor.Start();
            }
            catch (Exception exception)
            {
                _settings = settings with { Enabled = false };
                ShowError("PwaDrop could not start its drag monitor.", exception.HResult);
            }
        }
        else if (!settings.Enabled && _monitor.IsRunning)
        {
            _monitor.Stop();
        }

        _enabledMenuItem.Checked = _settings.Enabled;
        _enabledMenuItem.Text = _settings.Enabled ? "Bridge enabled" : "Bridge paused";
        ConfigureStartup(_settings.StartWithWindows);
        _settingsForm?.ApplySettings(_settings);
        SetStatus(_settings.Enabled ? "Bridge active" : "Bridge paused");
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
        var form = new SettingsForm(_settings, _cache.RootPath);
        form.SettingsChanged += settings => ApplySettings(settings);
        return form;
    }

    private void SetStatus(string status)
    {
        _settingsForm?.SetStatus(status);
        _trayIcon.Text = status.Length <= 63 ? $"PwaDrop — {status}" : "PwaDrop";
    }

    private void ShowError(string message, int errorCode)
    {
        _trayIcon.ShowBalloonTip(
            4000,
            "PwaDrop",
            $"{message} Error 0x{errorCode:X8}.",
            ToolTipIcon.Warning);
    }

    private void OpenCache()
    {
        Directory.CreateDirectory(_cache.RootPath);
        Process.Start(new ProcessStartInfo("explorer.exe", _cache.RootPath) { UseShellExecute = true });
    }

    private void OpenDiagnostics()
    {
        Directory.CreateDirectory(_dataPath);
        if (!File.Exists(_diagnostics.Path))
        {
            File.WriteAllText(_diagnostics.Path, string.Empty);
        }

        Process.Start(new ProcessStartInfo("notepad.exe", _diagnostics.Path) { UseShellExecute = true });
    }

    private static void ConfigureStartup(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: true) ??
                            Registry.CurrentUser.CreateSubKey(StartupRegistryPath, writable: true);
            if (enabled)
            {
                key.SetValue(StartupValueName, $"\"{Application.ExecutablePath}\"");
            }
            else
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Managed devices may deny the Run key. The UI remains usable manually.
        }
    }

    private void Exit()
    {
        _trayIcon.Visible = false;
        ExitThread();
    }
}
