using System.ComponentModel;
using System.Runtime.InteropServices;
using PwaDrop.App.Interop;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.App.Drag;

internal sealed class RelayOverlayForm : Form
{
    private const int ReleaseHideDelayMilliseconds = 250;
    private readonly OleRelayDropTarget _dropTarget;
    private readonly System.Windows.Forms.Timer _releaseHideTimer;
    private bool _registered;

    internal RelayOverlayForm(
        VirtualFileExtractor extractor,
        Func<ComTypes.IDataObject, uint, bool> prime,
        Func<ComTypes.IDataObject, NativeMethods.PointL, DragPayloadKind, bool> drop,
        Action leave,
        Action unsupported)
    {
        _dropTarget = new OleRelayDropTarget(extractor, prime, drop, leave, unsupported);
        _releaseHideTimer = new System.Windows.Forms.Timer
        {
            Interval = ReleaseHideDelayMilliseconds
        };
        _releaseHideTimer.Tick += (_, _) =>
        {
            _releaseHideTimer.Stop();
            HideRelay();
        };
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Opacity = 0.01;
        BackColor = Color.FromArgb(17, 24, 39);
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
    }

    internal bool IsRelayVisible => Visible;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterDropTarget();
    }

    private void RegisterDropTarget()
    {
        var result = NativeMethods.RegisterDragDrop(Handle, _dropTarget);
        _registered = result == 0;
        if (!_registered)
        {
            throw Marshal.GetExceptionForHR(result) ?? new InvalidOperationException("Unable to register the drag relay window.");
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_registered)
        {
            NativeMethods.RevokeDragDrop(Handle);
            _registered = false;
        }

        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _releaseHideTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    internal void ShowRelay()
    {
        _releaseHideTimer.Stop();
        if (Visible)
        {
            return;
        }

        Bounds = SystemInformation.VirtualScreen;
        Show();
        NativeMethods.SetWindowPos(
            Handle,
            new IntPtr(-1),
            Left,
            Top,
            Width,
            Height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
    }

    internal void HideRelay()
    {
        _releaseHideTimer.Stop();
        if (Visible)
        {
            Hide();
        }
    }

    internal void ScheduleHideAfterRelease()
    {
        _releaseHideTimer.Stop();
        _releaseHideTimer.Start();
    }

    internal void SuspendDropTarget()
    {
        HideRelay();
        if (!_registered)
        {
            return;
        }

        var result = NativeMethods.RevokeDragDrop(Handle);
        if (result < 0)
        {
            throw Marshal.GetExceptionForHR(result) ?? new InvalidOperationException("Unable to suspend the drag relay window.");
        }

        _registered = false;
    }

    internal void ResumeDropTarget()
    {
        if (_registered || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        RegisterDropTarget();
    }

    internal bool YieldToUnderlyingTarget(bool leftButtonDown)
    {
        return OriginalDragHandoff.TryWakeUnderlyingTarget(
            SuspendDropTarget,
            () => leftButtonDown,
            NudgeCursor);
    }

    private static void NudgeCursor()
    {
        var cursor = Cursor.Position;
        var screen = SystemInformation.VirtualScreen;
        var deltaX = cursor.X < screen.Right - 1 ? 1 : -1;
        var input = new NativeMethods.Input
        {
            Type = NativeMethods.InputMouse,
            Mouse = new NativeMethods.MouseInput
            {
                X = deltaX,
                Flags = NativeMethods.MouseEventMove
            }
        };

        if (NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>()) != 1)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }
}
