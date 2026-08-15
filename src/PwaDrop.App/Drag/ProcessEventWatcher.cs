using System.ComponentModel;
using PwaDrop.App.Interop;

namespace PwaDrop.App.Drag;

internal readonly record struct SourceProcessEvent(
    uint ProcessId,
    bool Started);

internal sealed class ProcessEventWatcher : IDisposable
{
    private readonly NativeMethods.WinEventProc _callback;
    private IntPtr _hook;
    private bool _disposed;

    internal ProcessEventWatcher()
    {
        _callback = OnWindowCreated;
    }

    internal event Action<SourceProcessEvent>? SourceProcessChanged;

    internal bool IsRunning => _hook != IntPtr.Zero;

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EventObjectCreate,
            NativeMethods.EventObjectCreate,
            IntPtr.Zero,
            _callback,
            0,
            0,
            NativeMethods.WinEventOutOfContext |
                NativeMethods.WinEventSkipOwnProcess);
        if (_hook == IntPtr.Zero)
        {
            throw new Win32Exception();
        }
    }

    internal void Stop()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        _ = NativeMethods.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private void OnWindowCreated(
        IntPtr hook,
        uint eventType,
        IntPtr hwnd,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        _ = hook;
        _ = eventType;
        _ = eventThread;
        _ = eventTime;
        if (hwnd == IntPtr.Zero ||
            objectId != NativeMethods.ObjIdWindow ||
            childId != NativeMethods.ChildIdSelf ||
            NativeMethods.GetAncestor(hwnd, NativeMethods.GaRoot) != hwnd)
        {
            return;
        }

        // A new top-level window can be a browser/Electron source or a host that
        // has just created a WebView2 process. Reconcile once after the burst.
        SourceProcessChanged?.Invoke(new SourceProcessEvent(0, Started: true));
    }
}
