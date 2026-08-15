using PwaDrop.AsyncDrag;

namespace PwaDrop.Core.Tests;

public sealed class AsyncDragOperationCoordinatorTests
{
    [Fact]
    public void Run_PrimesBeforeOriginalDrag_AndCompletesAfterward()
    {
        var events = new List<string>();
        var capability = new FakeCapability(events);

        var result = AsyncDragOperationCoordinator.Run(
            capability,
            () =>
            {
                events.Add("drag");
                return new AsyncDragResult(17, 1);
            });

        Assert.Equal(new AsyncDragResult(17, 1), result);
        Assert.Equal(["start", "drag", "end:17:1"], events);
        Assert.False(capability.InOperation);
    }

    [Fact]
    public void Run_DoesNotOwnAnOperationAlreadyStartedByAnotherParticipant()
    {
        var events = new List<string>();
        var capability = new FakeCapability(events) { InOperation = true };

        var result = AsyncDragOperationCoordinator.Run(
            capability,
            () =>
            {
                events.Add("drag");
                return new AsyncDragResult(23, 1);
            });

        Assert.Equal(new AsyncDragResult(23, 1), result);
        Assert.Equal(["drag"], events);
        Assert.True(capability.InOperation);
    }

    [Fact]
    public void Run_PassesThroughWhenAsyncModeIsUnavailable()
    {
        var events = new List<string>();
        var capability = new FakeCapability(events) { AsyncMode = false };

        var result = AsyncDragOperationCoordinator.Run(
            capability,
            () =>
            {
                events.Add("drag");
                return new AsyncDragResult(31, 0);
            });

        Assert.Equal(new AsyncDragResult(31, 0), result);
        Assert.Equal(["drag"], events);
    }

    [Fact]
    public void Run_CompletesOwnedOperationWhenOriginalDragThrows()
    {
        var events = new List<string>();
        var capability = new FakeCapability(events);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AsyncDragOperationCoordinator.Run(
                capability,
                () =>
                {
                    events.Add("drag");
                    throw new InvalidOperationException("simulated");
                }));

        Assert.Equal("simulated", exception.Message);
        Assert.Equal(["start", "drag", $"end:{unchecked((int)0x80004005)}:0"], events);
        Assert.False(capability.InOperation);
    }

    [Fact]
    public void Run_DoesNotCompleteWhenStartOperationFails()
    {
        var events = new List<string>();
        var capability = new FakeCapability(events) { StartSucceeds = false };

        var result = AsyncDragOperationCoordinator.Run(
            capability,
            () =>
            {
                events.Add("drag");
                return new AsyncDragResult(41, 0);
            });

        Assert.Equal(new AsyncDragResult(41, 0), result);
        Assert.Equal(["start", "drag"], events);
        Assert.False(capability.InOperation);
    }

    [Fact]
    public void Run_CompletesExactlyOnceAfterOwnedOperationEvenIfStateChanges()
    {
        var events = new List<string>();
        var capability = new FakeCapability(events);

        _ = AsyncDragOperationCoordinator.Run(
            capability,
            () =>
            {
                events.Add("drag");
                capability.InOperation = false;
                return new AsyncDragResult(51, 1);
            });

        Assert.Equal(["start", "drag", "end:51:1"], events);
        Assert.Equal(1, capability.CompletionCount);
    }

    private sealed class FakeCapability(List<string> events) : IAsyncDragCapability
    {
        public bool AsyncMode { get; set; } = true;

        public bool InOperation { get; set; }

        public bool StartSucceeds { get; set; } = true;

        public int CompletionCount { get; private set; }

        public bool TryStart()
        {
            events.Add("start");
            InOperation = StartSucceeds;
            return StartSucceeds;
        }

        public void Complete(int result, uint effect)
        {
            events.Add($"end:{result}:{effect}");
            CompletionCount++;
            InOperation = false;
        }
    }
}
