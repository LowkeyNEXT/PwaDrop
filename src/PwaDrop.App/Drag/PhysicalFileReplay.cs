using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PwaDrop.App.Interop;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.App.Drag;

internal static class PhysicalFileReplay
{
    private static readonly TimeSpan DragLoopWakeDelay = TimeSpan.FromMilliseconds(75);
    private static readonly TimeSpan DragLoopWakeInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan DropHoverDelay = TimeSpan.FromMilliseconds(300);

    internal static PhysicalReplayResult Replay(IReadOnlyList<string> files)
    {
        var dataObject = new DataObject();
        dataObject.SetData(DataFormats.FileDrop, autoConvert: true, files.ToArray());
        var comDataObject = (ComTypes.IDataObject)dataObject;
        uint effect = NativeMethods.DropEffectNone;
        var originalCursor = Cursor.Position;
        var nudgedCursor = GetNudgedCursorPosition(originalCursor);
        Point? injectedCursor = null;
        var wakeCount = 0;
        int result;
        try
        {
            result = RunWithWakeSignal(
                () =>
                {
                    var direction = (Interlocked.Increment(ref wakeCount) & 1) == 1
                        ? 1
                        : -1;
                    SendCursorInput(
                        (nudgedCursor.X - originalCursor.X) * direction,
                        (nudgedCursor.Y - originalCursor.Y) * direction);
                    injectedCursor = Cursor.Position;
                },
                () => NativeMethods.DoDragDrop(
                    comDataObject,
                    new ReleasedButtonDropSource(DropHoverDelay),
                    NativeMethods.DropEffectCopy,
                    out effect),
                DragLoopWakeDelay,
                DragLoopWakeInterval);
        }
        finally
        {
            if (injectedCursor is { } wakePosition &&
                Cursor.Position == wakePosition)
            {
                _ = NativeMethods.SetCursorPos(originalCursor.X, originalCursor.Y);
            }
        }

        return new PhysicalReplayResult(result, (DragDropEffects)effect);
    }

    internal static int RunWithWakeSignal(
        Action wakeSignal,
        Func<int> runDragLoop,
        TimeSpan wakeDelay,
        TimeSpan? repeatInterval = null)
    {
        using var canceled = new ManualResetEventSlim();
        Exception? wakeFailure = null;
        var wakeThread = new Thread(() =>
        {
            var delay = wakeDelay;
            while (!canceled.Wait(delay))
            {
                try
                {
                    wakeSignal();
                }
                catch (Exception exception)
                {
                    wakeFailure = exception;
                    break;
                }

                if (repeatInterval is not { } interval)
                {
                    break;
                }

                delay = interval;
            }
        })
        {
            IsBackground = true,
            Name = "PWADrop drag-loop wake"
        };

        wakeThread.Start();
        int result;
        try
        {
            result = runDragLoop();
        }
        finally
        {
            canceled.Set();
            wakeThread.Join();
        }

        if (wakeFailure is not null)
        {
            throw new InvalidOperationException("Unable to wake the physical replay drag loop.", wakeFailure);
        }

        return result;
    }

    private static Point GetNudgedCursorPosition(Point cursor)
    {
        var virtualScreen = SystemInformation.VirtualScreen;
        if (virtualScreen.Width > 1)
        {
            var nudgedX = cursor.X < virtualScreen.Right - 1
                ? cursor.X + 1
                : cursor.X - 1;
            return new Point(nudgedX, cursor.Y);
        }

        var nudgedY = cursor.Y < virtualScreen.Bottom - 1
            ? cursor.Y + 1
            : cursor.Y - 1;
        return new Point(cursor.X, nudgedY);
    }

    private static void SendCursorInput(int x, int y)
    {
        var input = new NativeMethods.Input
        {
            Type = NativeMethods.InputMouse,
            Mouse = new NativeMethods.MouseInput
            {
                X = x,
                Y = y,
                Flags = NativeMethods.MouseEventMove
            }
        };

        if (NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>()) != 1)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ReleasedButtonDropSource : IOleDropSource
    {
        private readonly long _dropAfter;

        internal ReleasedButtonDropSource(TimeSpan hoverDelay)
        {
            _dropAfter = Stopwatch.GetTimestamp() +
                         (long)(hoverDelay.TotalSeconds * Stopwatch.Frequency);
        }

        public int QueryContinueDrag(bool escapePressed, uint keyState)
        {
            if (escapePressed)
            {
                return unchecked((int)NativeMethods.DragDropSCancel);
            }

            if ((keyState & NativeMethods.MkLButton) != 0 ||
                Stopwatch.GetTimestamp() < _dropAfter)
            {
                return 0;
            }

            return unchecked((int)NativeMethods.DragDropSDrop);
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
