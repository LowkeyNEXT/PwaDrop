using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PwaDrop.App.Brand;

internal static class BrandIcon
{
    internal static Icon CreateIcon(int size = 64)
    {
        using var bitmap = CreateBitmap(size);
        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    internal static Bitmap CreateBitmap(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var inset = size * 0.08f;
        var bounds = new RectangleF(inset, inset, size - (2 * inset), size - (2 * inset));
        using var backgroundPath = RoundedRectangle(bounds, size * 0.22f);
        using var background = new LinearGradientBrush(
            bounds,
            Color.FromArgb(74, 106, 255),
            Color.FromArgb(125, 76, 255),
            45f);
        graphics.FillPath(background, backgroundPath);

        using var bridgePen = new Pen(Color.White, Math.Max(2f, size * 0.095f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var left = size * 0.28f;
        var right = size * 0.72f;
        var centerY = size * 0.50f;
        graphics.DrawLine(bridgePen, left, size * 0.34f, left, size * 0.66f);
        graphics.DrawLine(bridgePen, right, size * 0.34f, right, size * 0.66f);
        graphics.DrawBezier(
            bridgePen,
            left,
            centerY,
            size * 0.40f,
            size * 0.37f,
            size * 0.60f,
            size * 0.63f,
            right,
            centerY);
        return bitmap;
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
