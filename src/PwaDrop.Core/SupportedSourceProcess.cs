namespace PwaDrop.Core;

public static class SupportedSourceProcess
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "brave",
        "chrome",
        "chromium",
        "comet",
        "missive",
        "msedge",
        "msedgewebview2",
        "ms-teams",
        "msteams",
        "olk",
        "opera",
        "slack",
        "superhuman",
        "vivaldi",
        "Microsoft.OutlookForWindows",
        "PwaDrop.DragHarness"
    };

    public static bool IsSupported(string? executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return false;
        }

        var name = Path.GetFileName(executableName);
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 4);
        }

        return Names.Contains(name);
    }
}
