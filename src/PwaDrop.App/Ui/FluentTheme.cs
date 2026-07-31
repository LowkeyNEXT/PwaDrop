namespace PwaDrop.App.Ui;

internal static class FluentTheme
{
    internal static readonly Color Canvas = Color.FromArgb(11, 17, 35);
    internal static readonly Color Navigation = Color.FromArgb(8, 14, 29);
    internal static readonly Color Surface = Color.FromArgb(17, 26, 49);
    internal static readonly Color SurfaceHover = Color.FromArgb(25, 36, 63);
    internal static readonly Color SurfacePressed = Color.FromArgb(31, 44, 75);
    internal static readonly Color Stroke = Color.FromArgb(47, 58, 86);
    internal static readonly Color TextPrimary = Color.FromArgb(247, 248, 252);
    internal static readonly Color TextSecondary = Color.FromArgb(181, 188, 207);
    internal static readonly Color Accent = Color.FromArgb(74, 106, 255);
    internal static readonly Color AccentSecondary = Color.FromArgb(125, 76, 255);
    internal static readonly Color Success = Color.FromArgb(105, 220, 137);
    internal static readonly Color Warning = Color.FromArgb(255, 190, 76);
    internal static readonly Color Danger = Color.FromArgb(255, 101, 113);

    internal static Font Text(float size, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI Variable Text", size, style);

    internal static Font Display(float size, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI Variable Display", size, style);

    internal static Font Symbols(float size) => new("Segoe Fluent Icons", size);
}
