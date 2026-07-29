using System.Runtime.InteropServices;
using PwaDrop.App.Interop;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.App.Drag;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class OleRelayDropTarget : IOleDropTarget
{
    private readonly VirtualFileExtractor _extractor;
    private readonly Func<ComTypes.IDataObject, NativeMethods.PointL, DragPayloadKind, bool> _drop;
    private readonly Action _leave;
    private readonly Action _unsupported;
    private DragPayloadKind _payloadKind;

    internal OleRelayDropTarget(
        VirtualFileExtractor extractor,
        Func<ComTypes.IDataObject, NativeMethods.PointL, DragPayloadKind, bool> drop,
        Action leave,
        Action unsupported)
    {
        _extractor = extractor;
        _drop = drop;
        _leave = leave;
        _unsupported = unsupported;
    }

    public int DragEnter(ComTypes.IDataObject dataObject, uint keyState, NativeMethods.PointL point, ref uint effect)
    {
        _payloadKind = _extractor.DetectPayload(dataObject);
        effect = _payloadKind != DragPayloadKind.Unsupported
            ? NativeMethods.DropEffectCopy
            : NativeMethods.DropEffectNone;
        if (_payloadKind == DragPayloadKind.Unsupported)
        {
            _unsupported();
        }

        return 0;
    }

    public int DragOver(uint keyState, NativeMethods.PointL point, ref uint effect)
    {
        effect = _payloadKind != DragPayloadKind.Unsupported
            ? NativeMethods.DropEffectCopy
            : NativeMethods.DropEffectNone;
        return 0;
    }

    public int DragLeave()
    {
        _payloadKind = DragPayloadKind.Unsupported;
        _leave();
        return 0;
    }

    public int Drop(ComTypes.IDataObject dataObject, uint keyState, NativeMethods.PointL point, ref uint effect)
    {
        if (_payloadKind == DragPayloadKind.Unsupported)
        {
            effect = NativeMethods.DropEffectNone;
            return 0;
        }

        effect = _drop(dataObject, point, _payloadKind)
            ? NativeMethods.DropEffectCopy
            : NativeMethods.DropEffectNone;
        _payloadKind = DragPayloadKind.Unsupported;
        return 0;
    }
}
