using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PwaDrop.App.Brand;

internal static class BrandIcon
{
    private const string ResourceName = "PwaDrop.App.Assets.PwaDropLogo.png";

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
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName) ??
                           throw new InvalidOperationException($"Embedded brand asset {ResourceName} was not found.");
        using var source = Image.FromStream(stream);
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, size, size));
        return bitmap;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
