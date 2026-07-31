using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace PwaDrop.App.Ui;

internal sealed class FluentToggle : CheckBox
{
    internal FluentToggle()
    {
        Appearance = Appearance.Button;
        AutoSize = false;
        BackColor = FluentTheme.Canvas;
        FlatStyle = FlatStyle.Flat;
        Size = new Size(58, 32);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.CheckButton;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint |
                 ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnCheckedChanged(EventArgs eventArgs)
    {
        base.OnCheckedChanged(eventArgs);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        eventArgs.Graphics.Clear(FluentTheme.Canvas);

        var track = new RectangleF(1.5f, 2.5f, Width - 3f, Height - 5f);
        using var trackPath = RoundedRectangle(track, track.Height / 2f);
        using var trackBrush = new LinearGradientBrush(
            track,
            Checked ? FluentTheme.Accent : Color.FromArgb(27, 36, 59),
            Checked ? FluentTheme.AccentSecondary : Color.FromArgb(27, 36, 59),
            0f);
        using var borderPen = new Pen(Checked ? Color.FromArgb(118, 143, 255) : Color.FromArgb(107, 119, 148), 1.2f);
        eventArgs.Graphics.FillPath(trackBrush, trackPath);
        eventArgs.Graphics.DrawPath(borderPen, trackPath);

        var thumbSize = Height - 12f;
        var thumbX = Checked ? Width - thumbSize - 6f : 6f;
        var thumb = new RectangleF(thumbX, 6f, thumbSize, thumbSize);
        using var shadow = new SolidBrush(Color.FromArgb(45, 0, 0, 0));
        eventArgs.Graphics.FillEllipse(shadow, thumb.X, thumb.Y + 1.5f, thumb.Width, thumb.Height);
        using var thumbBrush = new SolidBrush(Color.White);
        eventArgs.Graphics.FillEllipse(thumbBrush, thumb);

        if (Focused && ShowFocusCues)
        {
            var focus = Rectangle.Inflate(ClientRectangle, -1, -1);
            ControlPaint.DrawFocusRectangle(eventArgs.Graphics, focus, FluentTheme.TextPrimary, FluentTheme.Canvas);
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class NavigationButton : Button
{
    private bool _selected;
    private bool _hot;

    internal NavigationButton(string text, string glyph)
    {
        Text = text;
        Glyph = glyph;
        AccessibleName = text;
        AccessibleRole = AccessibleRole.PushButton;
        AutoSize = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = FluentTheme.TextPrimary;
        Cursor = Cursors.Hand;
        Height = 58;
        TabStop = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint |
                 ControlStyles.SupportsTransparentBackColor, true);
    }

    internal string Glyph { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            AccessibleRole = value ? AccessibleRole.RadioButton : AccessibleRole.PushButton;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs eventArgs)
    {
        _hot = true;
        Invalidate();
        base.OnMouseEnter(eventArgs);
    }

    protected override void OnMouseLeave(EventArgs eventArgs)
    {
        _hot = false;
        Invalidate();
        base.OnMouseLeave(eventArgs);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        var fill = Selected
            ? FluentTheme.SurfacePressed
            : _hot ? FluentTheme.SurfaceHover : Color.Transparent;

        if (fill != Color.Transparent)
        {
            using var background = new SolidBrush(fill);
            using var path = RoundedRectangle(bounds, 7f);
            eventArgs.Graphics.FillPath(background, path);
            if (Selected)
            {
                using var stroke = new Pen(FluentTheme.Stroke, 1f);
                eventArgs.Graphics.DrawPath(stroke, path);
            }
        }

        if (Selected)
        {
            using var indicator = new SolidBrush(Color.FromArgb(63, 130, 255));
            using var path = RoundedRectangle(new RectangleF(3, 15, 3, 28), 1.5f);
            eventArgs.Graphics.FillPath(indicator, path);
        }

        using var iconFont = FluentTheme.Symbols(15f);
        using var textFont = FluentTheme.Text(10.8f);
        using var brush = new SolidBrush(FluentTheme.TextPrimary);
        eventArgs.Graphics.DrawString(Glyph, iconFont, brush, 17, 17);
        eventArgs.Graphics.DrawString(Text, textFont, brush, 60, 17);

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(eventArgs.Graphics, Rectangle.Inflate(bounds, -4, -4));
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, float radius) =>
        RoundedRectangle(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), radius);

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class FluentCard : Panel
{
    internal FluentCard()
    {
        BackColor = FluentTheme.Surface;
        DoubleBuffered = true;
        Padding = new Padding(24);
        Resize += (_, _) => UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedRectangle(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 9f);
        using var pen = new Pen(FluentTheme.Stroke, 1f);
        eventArgs.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        using var path = RoundedRectangle(new RectangleF(0, 0, Width, Height), 9f);
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class FluentToolStripRenderer : ToolStripProfessionalRenderer
{
    internal FluentToolStripRenderer() : base(new FluentToolStripColors())
    {
        RoundedEdges = true;
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs eventArgs)
    {
        using var font = FluentTheme.Symbols(11f);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            "\uE73E",
            font,
            eventArgs.ImageRectangle,
            FluentTheme.Accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private sealed class FluentToolStripColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => FluentTheme.Surface;
        public override Color ImageMarginGradientBegin => FluentTheme.Surface;
        public override Color ImageMarginGradientMiddle => FluentTheme.Surface;
        public override Color ImageMarginGradientEnd => FluentTheme.Surface;
        public override Color MenuBorder => FluentTheme.Stroke;
        public override Color MenuItemBorder => FluentTheme.Stroke;
        public override Color MenuItemSelected => FluentTheme.SurfaceHover;
        public override Color MenuItemSelectedGradientBegin => FluentTheme.SurfaceHover;
        public override Color MenuItemSelectedGradientEnd => FluentTheme.SurfaceHover;
        public override Color MenuItemPressedGradientBegin => FluentTheme.SurfacePressed;
        public override Color MenuItemPressedGradientEnd => FluentTheme.SurfacePressed;
        public override Color SeparatorDark => FluentTheme.Stroke;
        public override Color SeparatorLight => FluentTheme.Stroke;
    }
}
