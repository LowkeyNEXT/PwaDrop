namespace PwaDrop.Core;

public enum SourceProcessFamily
{
    None,
    ChromiumBrowser,
    WebView2,
    Electron
}

public readonly record struct SourceProcessInfo(
    uint ProcessId,
    uint ParentProcessId,
    string ExecutableName);

public static class SourceProcessPolicy
{
    private const int MaximumAncestorDepth = 8;

    private static readonly HashSet<string> ChromiumBrowsers = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "brave",
        "chrome",
        "chromium",
        "comet",
        "msedge",
        "opera",
        "vivaldi"
    };

    private static readonly HashSet<string> ElectronApplications = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "missive",
        "slack",
        "superhuman"
    };

    private static readonly HashSet<string> WebView2Hosts = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.OutlookForWindows",
        "ms-teams",
        "msteams",
        "olk"
    };

    public static bool IsCandidateExecutable(string? executableName)
    {
        var name = Normalize(executableName);
        return name.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase) ||
            ChromiumBrowsers.Contains(name) ||
            ElectronApplications.Contains(name);
    }

    public static SourceProcessFamily ClassifyRoot(
        uint processId,
        IReadOnlyDictionary<uint, SourceProcessInfo> processes)
    {
        if (!processes.TryGetValue(processId, out var source))
        {
            return SourceProcessFamily.None;
        }

        var name = Normalize(source.ExecutableName);
        if (ChromiumBrowsers.Contains(name))
        {
            return HasSameRuntimeParent(source, processes)
                ? SourceProcessFamily.None
                : SourceProcessFamily.ChromiumBrowser;
        }

        if (ElectronApplications.Contains(name))
        {
            return HasSameRuntimeParent(source, processes)
                ? SourceProcessFamily.None
                : SourceProcessFamily.Electron;
        }

        if (!name.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase) ||
            HasSameRuntimeParent(source, processes))
        {
            return SourceProcessFamily.None;
        }

        var current = source.ParentProcessId;
        for (var depth = 0; depth < MaximumAncestorDepth && current != 0; depth++)
        {
            if (!processes.TryGetValue(current, out var ancestor))
            {
                break;
            }

            var ancestorName = Normalize(ancestor.ExecutableName);
            if (ancestorName.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase))
            {
                return SourceProcessFamily.None;
            }

            if (WebView2Hosts.Contains(ancestorName))
            {
                return SourceProcessFamily.WebView2;
            }

            current = ancestor.ParentProcessId;
        }

        return SourceProcessFamily.None;
    }

    private static bool HasSameRuntimeParent(
        SourceProcessInfo source,
        IReadOnlyDictionary<uint, SourceProcessInfo> processes) =>
        processes.TryGetValue(source.ParentProcessId, out var parent) &&
        Normalize(parent.ExecutableName).Equals(
            Normalize(source.ExecutableName),
            StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return string.Empty;
        }

        var name = Path.GetFileName(executableName);
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
    }
}
