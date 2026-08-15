using System.Diagnostics;
using System.Runtime.InteropServices;
using PwaDrop.Core;

namespace PwaDrop.App.Interop;

internal static class ProcessClassifier
{
    internal static bool IsSupportedSourceWindow(IntPtr hwnd)
    {
        var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GaRoot);
        if (root == IntPtr.Zero)
        {
            root = hwnd;
        }

        NativeMethods.GetWindowThreadProcessId(root, out var processId);
        return IsSupportedSourceProcess(processId);
    }

    internal static bool IsSupportedSourceProcess(uint processId)
    {
        if (processId == 0)
        {
            return false;
        }

        string directName;
        try
        {
            directName = Process.GetProcessById((int)processId).ProcessName;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (SupportedSourceProcess.IsSupported(directName))
        {
            return true;
        }

        if (!directName.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase))
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

            var name = Path.GetFileNameWithoutExtension(process.ExecutableName);
            if (name.Equals("olk", StringComparison.OrdinalIgnoreCase) ||
                name.Equals(
                    "Microsoft.OutlookForWindows",
                    StringComparison.OrdinalIgnoreCase) ||
                name.Equals("ms-teams", StringComparison.OrdinalIgnoreCase) ||
                name.Equals(
                    "msteams",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = process.ParentProcessId;
        }

        return false;
    }

    internal static bool IsRootSupportedSourceProcess(
        uint processId,
        IReadOnlyDictionary<uint, SourceProcessInfo> processes) =>
        SourceProcessPolicy.ClassifyRoot(processId, processes) != SourceProcessFamily.None;

    internal static IReadOnlySet<uint> SnapshotTopLevelWindowProcessIds()
    {
        var processIds = new HashSet<uint>();
        _ = NativeMethods.EnumWindows((window, _) =>
        {
            if (NativeMethods.IsWindowVisible(window))
            {
                NativeMethods.GetWindowThreadProcessId(window, out var processId);
                if (processId != 0)
                {
                    processIds.Add(processId);
                }
            }

            return true;
        }, IntPtr.Zero);
        return processIds;
    }

    internal static IReadOnlyDictionary<uint, SourceProcessInfo> SnapshotProcessTree()
    {
        var result = new Dictionary<uint, SourceProcessInfo>();
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
                result[entry.ProcessId] = new SourceProcessInfo(
                    entry.ProcessId,
                    entry.ParentProcessId,
                    entry.ExeFile ?? string.Empty);
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
}
