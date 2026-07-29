using System.Runtime.InteropServices;
using PwaDrop.App.Interop;

namespace PwaDrop.App.Drag;

internal sealed class OutlookDragMonitor : IDisposable
{
    private readonly RelayOverlayForm _overlay;
    private readonly Func<IReadOnlyList<IntPtr>> _excludedWindows;
    private readonly NativeMethods.HookProc _callback;
    private IntPtr _hook;
    private IntPtr _sourceRoot;
    private NativeMethods.Point _startPoint;
    private bool _thresholdPassed;

    internal OutlookDragMonitor(RelayOverlayForm overlay, Func<IReadOnlyList<IntPtr>> excludedWindows)
    {
        _overlay = overlay;
        _excludedWindows = excludedWindows;
        _callback = HookCallback;
    }

    internal bool IsRunning => _hook != IntPtr.Zero;

    internal void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _callback, IntPtr.Zero, 0);
        if (_hook == IntPtr.Zero)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Unable to monitor New Outlook drags.");
        }
    }

    internal void Stop()
    {
        _overlay.HideRelay();
        _sourceRoot = IntPtr.Zero;
        _thresholdPassed = false;
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0)
        {
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        var message = unchecked((int)(long)wParam);
        var hookData = Marshal.PtrToStructure<NativeMethods.MsllHookStruct>(lParam);

        switch (message)
        {
            case NativeMethods.WmLButtonDown:
                BeginCandidate(hookData.Point);
                break;
            case NativeMethods.WmMouseMove:
                TrackCandidate(hookData.Point);
                break;
            case NativeMethods.WmLButtonUp:
                _sourceRoot = IntPtr.Zero;
                _thresholdPassed = false;
                break;
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void BeginCandidate(NativeMethods.Point point)
    {
        var window = NativeMethods.WindowFromPoint(point);
        if (window == IntPtr.Zero || !ProcessClassifier.IsNewOutlookWindow(window))
        {
            _sourceRoot = IntPtr.Zero;
            return;
        }

        _sourceRoot = NativeMethods.GetAncestor(window, NativeMethods.GaRoot);
        _startPoint = point;
        _thresholdPassed = false;
    }

    private void TrackCandidate(NativeMethods.Point point)
    {
        if (_sourceRoot == IntPtr.Zero || (NativeMethods.GetAsyncKeyState(NativeMethods.VkLButton) & 0x8000) == 0)
        {
            return;
        }

        if (!_thresholdPassed)
        {
            var dragSize = SystemInformation.DragSize;
            _thresholdPassed = Math.Abs(point.X - _startPoint.X) >= dragSize.Width / 2 ||
                               Math.Abs(point.Y - _startPoint.Y) >= dragSize.Height / 2;
            if (!_thresholdPassed)
            {
                return;
            }
        }

        var excluded = _excludedWindows().Append(_overlay.Handle).Distinct().ToArray();
        var target = _overlay.IsRelayVisible
            ? WindowSearch.FindTopLevelAtPoint(point, excluded)
            : NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(point), NativeMethods.GaRoot);

        if (target == IntPtr.Zero || target == _sourceRoot || ProcessClassifier.IsNewOutlookWindow(target))
        {
            _overlay.HideRelay();
            return;
        }

        _overlay.ShowRelay();
    }
}

