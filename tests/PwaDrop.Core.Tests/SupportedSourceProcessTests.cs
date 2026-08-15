using PwaDrop.Core;

namespace PwaDrop.Core.Tests;

public sealed class SupportedSourceProcessTests
{
    [Theory]
    [InlineData("msedge.exe")]
    [InlineData("chrome")]
    [InlineData("msedgewebview2.exe")]
    [InlineData("brave.exe")]
    [InlineData("comet.exe")]
    [InlineData("olk.exe")]
    [InlineData("slack.exe")]
    [InlineData("missive.exe")]
    [InlineData("superhuman.exe")]
    [InlineData("ms-teams.exe")]
    [InlineData("Microsoft.OutlookForWindows.exe")]
    [InlineData("Microsoft.OutlookForWindows")]
    [InlineData("PwaDrop.DragHarness.exe")]
    [InlineData("PwaDrop.DragHarness")]
    public void IsSupported_AcceptsChromiumAndWebViewSources(string executableName)
    {
        Assert.True(SupportedSourceProcess.IsSupported(executableName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("explorer.exe")]
    [InlineData("notepad.exe")]
    public void IsSupported_RejectsUnrelatedProcesses(string? executableName)
    {
        Assert.False(SupportedSourceProcess.IsSupported(executableName));
    }

    [Theory]
    [InlineData("msedge.exe")]
    [InlineData("chrome.exe")]
    [InlineData("brave.exe")]
    [InlineData("chromium.exe")]
    [InlineData("opera.exe")]
    [InlineData("vivaldi.exe")]
    [InlineData("comet.exe")]
    public void ClassifyRoot_AcceptsChromiumBrowserRoots(string executableName)
    {
        var processes = Tree(
            new(100, 50, executableName),
            new(50, 1, "explorer.exe"));

        Assert.Equal(
            SourceProcessFamily.ChromiumBrowser,
            SourceProcessPolicy.ClassifyRoot(100, processes));
    }

    [Theory]
    [InlineData("slack.exe")]
    [InlineData("missive.exe")]
    [InlineData("superhuman.exe")]
    public void ClassifyRoot_AcceptsElectronAppRoots(string executableName)
    {
        var processes = Tree(
            new(100, 50, executableName),
            new(50, 1, "explorer.exe"));

        Assert.Equal(
            SourceProcessFamily.Electron,
            SourceProcessPolicy.ClassifyRoot(100, processes));
    }

    [Theory]
    [InlineData("olk.exe")]
    [InlineData("Microsoft.OutlookForWindows.exe")]
    [InlineData("ms-teams.exe")]
    [InlineData("msteams.exe")]
    public void ClassifyRoot_AcceptsWebView2RootUnderSupportedHost(string hostName)
    {
        var processes = Tree(
            new(100, 75, "msedgewebview2.exe"),
            new(75, 50, hostName),
            new(50, 1, "explorer.exe"));

        Assert.Equal(
            SourceProcessFamily.WebView2,
            SourceProcessPolicy.ClassifyRoot(100, processes));
    }

    [Theory]
    [InlineData("chrome.exe")]
    [InlineData("slack.exe")]
    [InlineData("msedgewebview2.exe")]
    public void ClassifyRoot_RejectsChildProcessesFromSameRuntime(string executableName)
    {
        var processes = Tree(
            new(101, 100, executableName),
            new(100, 50, executableName),
            new(50, 1, "explorer.exe"));

        Assert.Equal(
            SourceProcessFamily.None,
            SourceProcessPolicy.ClassifyRoot(101, processes));
    }

    [Fact]
    public void ClassifyRoot_RejectsWebView2UnderUnknownHost()
    {
        var processes = Tree(
            new(100, 50, "msedgewebview2.exe"),
            new(50, 1, "SomeRandomApp.exe"));

        Assert.Equal(
            SourceProcessFamily.None,
            SourceProcessPolicy.ClassifyRoot(100, processes));
    }

    [Theory]
    [InlineData("explorer.exe")]
    [InlineData("notepad.exe")]
    [InlineData("valorant.exe")]
    [InlineData("vgc.exe")]
    public void ClassifyRoot_RejectsUnrelatedAndGameProcesses(string executableName)
    {
        var processes = Tree(new SourceProcessInfo(100, 50, executableName));

        Assert.Equal(
            SourceProcessFamily.None,
            SourceProcessPolicy.ClassifyRoot(100, processes));
    }

    private static IReadOnlyDictionary<uint, SourceProcessInfo> Tree(
        params SourceProcessInfo[] processes) =>
        processes.ToDictionary(process => process.ProcessId);
}
