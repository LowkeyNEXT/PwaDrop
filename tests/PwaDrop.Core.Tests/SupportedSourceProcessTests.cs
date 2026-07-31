using PwaDrop.Core;

namespace PwaDrop.Core.Tests;

public sealed class SupportedSourceProcessTests
{
    [Theory]
    [InlineData("msedge.exe")]
    [InlineData("chrome")]
    [InlineData("msedgewebview2.exe")]
    [InlineData("brave.exe")]
    [InlineData("olk.exe")]
    [InlineData("Microsoft.OutlookForWindows.exe")]
    [InlineData("PwaDrop.DragHarness.exe")]
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
}
