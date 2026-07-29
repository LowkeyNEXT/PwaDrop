using System.Runtime.InteropServices;
using PwaDrop.App.Interop;

namespace PwaDrop.DragHarness;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class MouseDropSource : IOleDropSource
{
    public int QueryContinueDrag(bool escapePressed, uint keyState)
    {
        if (escapePressed)
        {
            return unchecked((int)NativeMethods.DragDropSCancel);
        }

        return (keyState & NativeMethods.MkLButton) == 0
            ? unchecked((int)NativeMethods.DragDropSDrop)
            : 0;
    }

    public int GiveFeedback(uint effect) => unchecked((int)NativeMethods.DragDropSUseDefaultCursors);
}
