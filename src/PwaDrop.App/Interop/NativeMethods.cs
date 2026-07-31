using System.Runtime.InteropServices;
using System.Text;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.App.Interop;

internal static class NativeMethods
{
    internal const int WhMouseLl = 14;
    internal const int WmMouseMove = 0x0200;
    internal const int WmLButtonDown = 0x0201;
    internal const int WmLButtonUp = 0x0202;
    internal const int WmNcHitTest = 0x0084;
    internal const int VkLButton = 0x01;
    internal const int GaRoot = 2;
    internal const int SwpNoActivate = 0x0010;
    internal const int SwpShowWindow = 0x0040;
    internal const int WsExToolWindow = 0x00000080;
    internal const int WsExNoActivate = 0x08000000;
    internal const int DwmwaUseImmersiveDarkMode = 20;
    internal const int DwmwaWindowCornerPreference = 33;
    internal const int DwmwaSystemBackdropType = 38;
    internal const int HtClient = 1;
    internal const int HtCaption = 2;
    internal const int HtLeft = 10;
    internal const int HtRight = 11;
    internal const int HtTop = 12;
    internal const int HtTopLeft = 13;
    internal const int HtTopRight = 14;
    internal const int HtBottom = 15;
    internal const int HtBottomLeft = 16;
    internal const int HtBottomRight = 17;
    internal const uint DropEffectNone = 0;
    internal const uint DropEffectCopy = 1;
    internal const uint DragDropSCancel = 0x00040101;
    internal const uint DragDropSDrop = 0x00040100;
    internal const uint DragDropSUseDefaultCursors = 0x00040102;
    internal const uint MkLButton = 0x0001;
    internal const uint TymedHGlobal = 1;
    internal const uint TymedIStream = 4;
    internal const uint DvAspectContent = 1;
    internal const short CfHDrop = 15;
    internal const uint DragQueryFileCount = 0xFFFFFFFF;
    internal const uint Th32CsSnapProcess = 0x00000002;

    internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct Point(int x, int y)
    {
        internal readonly int X = x;
        internal readonly int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct PointL(int x, int y)
    {
        internal readonly int X = x;
        internal readonly int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;

        internal readonly bool Contains(Point point) =>
            point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MsllHookStruct
    {
        internal Point Point;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ProcessEntry32
    {
        internal uint Size;
        internal uint Usage;
        internal uint ProcessId;
        internal UIntPtr DefaultHeapId;
        internal uint ModuleId;
        internal uint Threads;
        internal uint ParentProcessId;
        internal int BasePriority;
        internal uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        internal string ExeFile;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("ole32.dll")]
    internal static extern int RegisterDragDrop(IntPtr hwnd, [MarshalAs(UnmanagedType.Interface)] IOleDropTarget dropTarget);

    [DllImport("ole32.dll")]
    internal static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll")]
    internal static extern void OleUninitialize();

    [DllImport("ole32.dll")]
    internal static extern int RevokeDragDrop(IntPtr hwnd);

    [DllImport("ole32.dll")]
    internal static extern int DoDragDrop(
        [MarshalAs(UnmanagedType.Interface)] ComTypes.IDataObject dataObject,
        [MarshalAs(UnmanagedType.Interface)] IOleDropSource dropSource,
        uint allowedEffects,
        out uint effect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern ushort RegisterClipboardFormat(string format);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern UIntPtr GlobalSize(IntPtr memory);

    [DllImport("ole32.dll")]
    internal static extern void ReleaseStgMedium(ref ComTypes.STGMEDIUM medium);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint DragQueryFile(
        IntPtr dropHandle,
        uint fileIndex,
        StringBuilder? filePath,
        uint characterCount);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}

[ComVisible(true)]
[Guid("00000122-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleDropTarget
{
    [PreserveSig]
    int DragEnter(
        [MarshalAs(UnmanagedType.Interface)] ComTypes.IDataObject dataObject,
        uint keyState,
        NativeMethods.PointL point,
        ref uint effect);

    [PreserveSig]
    int DragOver(uint keyState, NativeMethods.PointL point, ref uint effect);

    [PreserveSig]
    int DragLeave();

    [PreserveSig]
    int Drop(
        [MarshalAs(UnmanagedType.Interface)] ComTypes.IDataObject dataObject,
        uint keyState,
        NativeMethods.PointL point,
        ref uint effect);
}

[ComVisible(true)]
[Guid("00000121-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleDropSource
{
    [PreserveSig]
    int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escapePressed, uint keyState);

    [PreserveSig]
    int GiveFeedback(uint effect);
}

[ComImport]
[Guid("3D8B0590-F691-11D2-8EA9-006097DF5BD4")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDataObjectAsyncCapability
{
    [PreserveSig]
    int SetAsyncMode([MarshalAs(UnmanagedType.Bool)] bool asyncMode);

    [PreserveSig]
    int GetAsyncMode([MarshalAs(UnmanagedType.Bool)] out bool asyncMode);

    [PreserveSig]
    int StartOperation([MarshalAs(UnmanagedType.Interface)] object? reserved);

    [PreserveSig]
    int InOperation([MarshalAs(UnmanagedType.Bool)] out bool inAsyncOperation);

    [PreserveSig]
    int EndOperation(int result, [MarshalAs(UnmanagedType.Interface)] object? reserved, uint effects);
}
