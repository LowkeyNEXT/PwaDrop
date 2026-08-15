namespace PwaDrop.App.Drag;

internal static class OriginalDragHandoff
{
    internal static bool TryWakeUnderlyingTarget(
        Action hideAndSuspend,
        Func<bool> isLeftButtonDown,
        Action nudgeCursor)
    {
        hideAndSuspend();
        if (!isLeftButtonDown())
        {
            return false;
        }

        nudgeCursor();
        return true;
    }
}
