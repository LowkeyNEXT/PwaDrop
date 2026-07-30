using System.Runtime.InteropServices;
using PwaDrop.App.Interop;

namespace PwaDrop.App.Drag;

internal sealed class OutlookDragMonitor : IDisposable
{
    private readonly RelayOverlayForm _overlay;
    private readonly Func<IReadOnlyList<IntPtr>> _excludedWindows;
    private readonly Action _primedDragReleased;
    private readonly NativeMethods.HookProc _callback;
    private IntPtr _hook;
    private IntPtr _sourceRoot;
    private uint _sourceProcessId;
    private NativeMethods.Point _startPoint;
    private bool _thresholdPassed;
    private bool _currentDragPrimed;

    internal OutlookDragMonitor(
        RelayOverlayForm overlay,
        Func<IReadOnlyList<IntPtr>> excludedWindows,
        Action primedDragReleased)
    {
        _overlay = overlay;
        _excludedWindows = excludedWindows;
        _primedDragReleased = primedDragReleased;
        _callback = HookCallback;
    }

    internal bool IsRunning => _hook != IntPtr.Zero;

    internal void MarkCurrentDragPrimed()
    {
        _currentDragPrimed = true;
    }

    internal void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhMouseLl,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);
        if (_hook == IntPtr.Zero)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Unable to monitor New Outlook drags.");
        }
    }

    internal void Stop()
    {
        _overlay.HideRelay();
        _sourceRoot = IntPtr.Zero;
        _sourceProcessId = 0;
        _thresholdPassed = false;
        _currentDragPrimed = false;
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
                _currentDragPrimed = false;
                BeginCandidate(hookData.Point);
                break;
            case NativeMethods.WmMouseMove:
                TrackCandidate(hookData.Point);
                break;
            case NativeMethods.WmLButtonUp:
                var primedDragReleased = _currentDragPrimed;
                _sourceRoot = IntPtr.Zero;
                _sourceProcessId = 0;
                _thresholdPassed = false;
                _currentDragPrimed = false;
                if (primedDragReleased)
                {
                    _primedDragReleased();
                }

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
            _sourceProcessId = 0;
            return;
        }

        _sourceRoot = NativeMethods.GetAncestor(window, NativeMethods.GaRoot);
        NativeMethods.GetWindowThreadProcessId(_sourceRoot, out _sourceProcessId);
        _startPoint = point;
        _thresholdPassed = false;
    }

    private void TrackCandidate(NativeMethods.Point point)
    {
        if (_currentDragPrimed)
        {
            _overlay.HideRelay();
            return;
        }

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

        NativeMethods.GetWindowThreadProcessId(target, out var targetProcessId);
        if (target == IntPtr.Zero || target == _sourceRoot || targetProcessId == _sourceProcessId)
        {
            _overlay.HideRelay();
            return;
        }

        _overlay.ShowRelay();
    }
}
