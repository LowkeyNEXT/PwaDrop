namespace PwaDrop.Core;

public static class SupportedSourceProcess
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "brave",
        "chrome",
        "chromium",
        "msedge",
        "msedgewebview2",
        "olk",
        "opera",
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

        return Names.Contains(Path.GetFileNameWithoutExtension(executableName));
    }
}
