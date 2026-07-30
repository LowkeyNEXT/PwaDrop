using System.Runtime.InteropServices;
using PwaDrop.App.Interop;

namespace PwaDrop.DragHarness;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class MouseDropSource : IOleDropSource
{
    private readonly VirtualFileDataObject _dataObject;

    internal MouseDropSource(VirtualFileDataObject dataObject)
    {
        _dataObject = dataObject;
    }

    public int QueryContinueDrag(bool escapePressed, uint keyState)
    {
        if (escapePressed)
        {
            _dataObject.FinishDragLoop();
            return unchecked((int)NativeMethods.DragDropSCancel);
        }

        if ((keyState & NativeMethods.MkLButton) != 0)
        {
            return 0;
        }

        _dataObject.FinishDragLoop();
        return unchecked((int)NativeMethods.DragDropSDrop);
    }

    public int GiveFeedback(uint effect) => unchecked((int)NativeMethods.DragDropSUseDefaultCursors);
}
