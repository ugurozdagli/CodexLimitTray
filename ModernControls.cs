using System.Drawing.Drawing2D;

namespace CodexLimitTray;

internal static class ModernShape
{
    public static void Prepare(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    }
    public static GraphicsPath Round(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2);
        if (radius <= 0) { path.AddRectangle(rect); return path; }
        var d = radius * 2;
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
}

internal sealed class ModernButton : Button
{
    private bool hover;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }
    public ModernButton()
    {
        Cursor = Cursors.Hand; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        BackColor = Palette.Background;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        MouseEnter += (_, _) => { hover = true; Invalidate(); };
        MouseLeave += (_, _) => { hover = false; Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; ModernShape.Prepare(g); g.Clear(BackColor);
        using var shape = ModernShape.Round(new RectangleF(1, 1, Width - 2, Height - 2), 11);
        var color = Primary ? (hover ? Color.FromArgb(132, 242, 205) : Palette.Mint) : (hover ? Color.FromArgb(42, 54, 71) : Color.FromArgb(29, 39, 53));
        if (!Enabled) color = Color.FromArgb(40, 54, 60);
        using var fill = new SolidBrush(color); g.FillPath(fill, shape);
        if (!Primary) { using var border = new Pen(Color.FromArgb(49, 64, 83)); g.DrawPath(border, shape); }
        using var font = Palette.Font(Text == "×" ? 14 : 9.25f, FontStyle.Bold);
        using var textBrush = new SolidBrush(!Enabled ? Palette.Muted : Primary ? Color.FromArgb(15, 44, 35) : Palette.Text);
        using var textFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(Text, font, textBrush, ClientRectangle, textFormat);
        if (Focused && ShowFocusCues) { using var ring = new Pen(Palette.Text); g.DrawPath(ring, shape); }
    }
}

internal sealed class ModernToggle : CheckBox
{
    public ModernToggle()
    {
        Text = L10n.T("Windows ile başlat"); AutoSize = false; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; ModernShape.Prepare(g); g.Clear(BackColor);
        using var font = Palette.Font(9.5f);
        using var label = new SolidBrush(Palette.Muted);
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(Text, font, label, new RectangleF(0, 0, Width - 52, Height), format);
        var x = Width - 38; var y = (Height - 20) / 2;
        using var path = ModernShape.Round(new RectangleF(x, y, 36, 20), 10);
        using var fill = new SolidBrush(Checked ? Palette.Mint : Color.FromArgb(56, 69, 85)); g.FillPath(fill, path);
        using var knob = new SolidBrush(Checked ? Color.FromArgb(17, 53, 41) : Color.FromArgb(206, 217, 230));
        g.FillEllipse(knob, x + (Checked ? 18 : 3), y + 3, 14, 14);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, ClientRectangle, Palette.Muted, BackColor);
    }
}

internal sealed class SmoothLabel : Label
{
    public SmoothLabel() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    protected override void OnPaint(PaintEventArgs e)
    {
        ModernShape.Prepare(e.Graphics);
        using var brush = new SolidBrush(ForeColor);
        e.Graphics.DrawString(Text, Font, brush, ClientRectangle);
    }
}
