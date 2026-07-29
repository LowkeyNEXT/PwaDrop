using System.Runtime.InteropServices;
using PwaDrop.App.Interop;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.App.Drag;

internal sealed class RelayOverlayForm : Form
{
    private readonly OleRelayDropTarget _dropTarget;
    private bool _registered;

    internal RelayOverlayForm(
        VirtualFileExtractor extractor,
        Func<ComTypes.IDataObject, NativeMethods.PointL, bool> drop,
        Action leave)
    {
        _dropTarget = new OleRelayDropTarget(extractor, drop, leave);
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

    internal void ShowRelay()
    {
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
        if (Visible)
        {
            Hide();
        }
    }
}
