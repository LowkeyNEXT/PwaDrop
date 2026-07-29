using System.Runtime.InteropServices;
using PwaDrop.App.Interop;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.App.Drag;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class OleRelayDropTarget : IOleDropTarget
{
    private readonly VirtualFileExtractor _extractor;
    private readonly Action<ComTypes.IDataObject, NativeMethods.PointL> _drop;
    private readonly Action _leave;
    private bool _supported;

    internal OleRelayDropTarget(
        VirtualFileExtractor extractor,
        Action<ComTypes.IDataObject, NativeMethods.PointL> drop,
        Action leave)
    {
        _extractor = extractor;
        _drop = drop;
        _leave = leave;
    }

    public int DragEnter(ComTypes.IDataObject dataObject, uint keyState, NativeMethods.PointL point, ref uint effect)
    {
        _supported = _extractor.CanExtract(dataObject);
        effect = _supported ? NativeMethods.DropEffectCopy : NativeMethods.DropEffectNone;
        return 0;
    }

    public int DragOver(uint keyState, NativeMethods.PointL point, ref uint effect)
    {
        effect = _supported ? NativeMethods.DropEffectCopy : NativeMethods.DropEffectNone;
        return 0;
    }

    public int DragLeave()
    {
        _supported = false;
        _leave();
        return 0;
    }

    public int Drop(ComTypes.IDataObject dataObject, uint keyState, NativeMethods.PointL point, ref uint effect)
    {
        if (!_supported)
        {
            effect = NativeMethods.DropEffectNone;
            return 0;
        }

        effect = NativeMethods.DropEffectCopy;
        _drop(dataObject, point);
        _supported = false;
        return 0;
    }
}

