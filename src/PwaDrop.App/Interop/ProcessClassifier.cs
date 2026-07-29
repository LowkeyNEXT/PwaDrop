using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PwaDrop.App.Interop;

internal static class ProcessClassifier
{
    internal static bool IsNewOutlookWindow(IntPtr hwnd)
    {
        var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GaRoot);
        if (root == IntPtr.Zero)
        {
            root = hwnd;
        }

        NativeMethods.GetWindowThreadProcessId(root, out var processId);
        return IsNewOutlookProcess(processId);
    }

    internal static bool IsNewOutlookProcess(uint processId)
    {
        if (processId == 0)
        {
            return false;
        }

        var processNames = SnapshotProcessTree();
        var current = processId;
        for (var depth = 0; depth < 8 && current != 0; depth++)
        {
            if (!processNames.TryGetValue(current, out var process))
            {
                break;
            }

            var name = Path.GetFileNameWithoutExtension(process.Executable);
            if (name.Equals("olk", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Microsoft.OutlookForWindows", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("PwaDrop.DragHarness", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = process.ParentId;
        }

        try
        {
            return Process.GetProcessById((int)processId).ProcessName.Equals("olk", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static Dictionary<uint, ProcessInfo> SnapshotProcessTree()
    {
        var result = new Dictionary<uint, ProcessInfo>();
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.Th32CsSnapProcess, 0);
        if (snapshot == new IntPtr(-1))
        {
            return result;
        }

        try
        {
            var entry = new NativeMethods.ProcessEntry32
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.ProcessEntry32>()
            };

            if (!NativeMethods.Process32First(snapshot, ref entry))
            {
                return result;
            }

            do
            {
                result[entry.ProcessId] = new ProcessInfo(entry.ParentProcessId, entry.ExeFile ?? string.Empty);
                entry.Size = (uint)Marshal.SizeOf<NativeMethods.ProcessEntry32>();
            }
            while (NativeMethods.Process32Next(snapshot, ref entry));
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }

        return result;
    }

    private readonly record struct ProcessInfo(uint ParentId, string Executable);
}
