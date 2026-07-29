using System.Runtime.InteropServices;
using PwaDrop.App.Interop;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.App.Drag;

internal static class PhysicalFileReplay
{
    internal static PhysicalReplayResult Replay(IReadOnlyList<string> files)
    {
        var dataObject = new DataObject();
        dataObject.SetData(DataFormats.FileDrop, autoConvert: true, files.ToArray());
        var comDataObject = (ComTypes.IDataObject)dataObject;
        var result = NativeMethods.DoDragDrop(
            comDataObject,
            new ReleasedButtonDropSource(),
            NativeMethods.DropEffectCopy,
            out var effect);

        return new PhysicalReplayResult(result, (DragDropEffects)effect);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ReleasedButtonDropSource : IOleDropSource
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
}

internal readonly record struct PhysicalReplayResult(int HResult, DragDropEffects Effect)
{
    internal bool Accepted =>
        (HResult == 0 || unchecked((uint)HResult) == NativeMethods.DragDropSDrop) &&
        (Effect & DragDropEffects.Copy) == DragDropEffects.Copy;
}
