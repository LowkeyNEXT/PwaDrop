namespace PwaDrop.AsyncDrag;

public interface IAsyncDragCapability
{
    bool AsyncMode { get; }

    bool InOperation { get; }

    bool TryStart();

    void Complete(int result, uint effect);
}

public readonly struct AsyncDragResult : IEquatable<AsyncDragResult>
{
    public AsyncDragResult(int hResult, uint effect)
    {
        HResult = hResult;
        Effect = effect;
    }

    public int HResult { get; }

    public uint Effect { get; }

    public bool Equals(AsyncDragResult other) =>
        HResult == other.HResult && Effect == other.Effect;

    public override bool Equals(object? obj) =>
        obj is AsyncDragResult other && Equals(other);

    public override int GetHashCode() =>
        (HResult * 397) ^ (int)Effect;

    public override string ToString() =>
        $"AsyncDragResult {{ HResult = {HResult}, Effect = {Effect} }}";
}

public static class AsyncDragOperationCoordinator
{
    private const int EFail = unchecked((int)0x80004005);

    public static AsyncDragResult Run(
        IAsyncDragCapability capability,
        Func<AsyncDragResult> runOriginalDrag)
    {
        if (!capability.AsyncMode ||
            capability.InOperation ||
            !capability.TryStart())
        {
            return runOriginalDrag();
        }

        AsyncDragResult result;
        try
        {
            result = runOriginalDrag();
        }
        catch
        {
            capability.Complete(EFail, 0);
            throw;
        }

        capability.Complete(result.HResult, result.Effect);
        return result;
    }
}
