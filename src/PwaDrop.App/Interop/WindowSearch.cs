namespace PwaDrop.App.Interop;

internal static class WindowSearch
{
    internal static IntPtr FindTopLevelAtPoint(NativeMethods.Point point, params IntPtr[] excludedWindows)
    {
        var excluded = excludedWindows.ToHashSet();
        var result = IntPtr.Zero;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (excluded.Contains(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
            {
                return true;
            }

            if (NativeMethods.GetWindowRect(hwnd, out var rect) && rect.Contains(point))
            {
                result = hwnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }
}
