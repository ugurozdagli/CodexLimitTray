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
        var color = Primary ? (hover ? ControlPaint.Light(Palette.Mint, .1f) : Palette.Mint) : (hover ? Palette.Chip : Palette.Card);
        if (!Enabled) color = Palette.Track;
        using var fill = new SolidBrush(color); g.FillPath(fill, shape);
        if (!Primary) { using var border = new Pen(Palette.Border); g.DrawPath(border, shape); }
        using var font = Palette.Font(Text == "×" ? 14 : 9.25f, FontStyle.Bold);
        using var textBrush = new SolidBrush(!Enabled ? Palette.Muted : Primary ? Palette.PrimaryText : Palette.Text);
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
        e.Graphics.DrawString(Text, Font, brush, new RectangleF(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical));
    }
}

internal sealed class ModernSelect : Control
{
    public List<object> Items { get; } = new();
    private int selected;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex { get => selected; set { selected = value; Invalidate(); } }
    public object? SelectedItem => Items.ElementAtOrDefault(selected);
    public ModernSelect()
    {
        DoubleBuffered = true; Cursor = Cursors.Hand; TabStop = true; AccessibleRole = AccessibleRole.ComboBox;
        Click += (_, _) => OpenChoices();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Space or Keys.Enter) { OpenChoices(); e.Handled = true; }
            else if (e.KeyCode is Keys.Up or Keys.Down) { SelectedIndex = Math.Clamp(selected + (e.KeyCode == Keys.Up ? -1 : 1), 0, Items.Count - 1); e.Handled = true; }
        };
        GotFocus += (_, _) => Invalidate(); LostFocus += (_, _) => Invalidate();
    }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Space or Keys.Enter || base.IsInputKey(keyData);
    protected override AccessibleObject CreateAccessibilityInstance() => new SelectionAccessibility(this);
    private sealed class SelectionAccessibility(ModernSelect owner) : ControlAccessibleObject(owner)
    {
        public override string? Value { get => owner.SelectedItem?.ToString(); set { } }
        public override string? DefaultAction => L10n.Text("Seçenekleri aç", "Open choices");
        public override void DoDefaultAction() => owner.OpenChoices();
    }
    private void OpenChoices()
    {
        Focus();
        var menu = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new MenuColors()), BackColor = Palette.Background, ForeColor = Palette.Text, Font = Palette.Font(9) };
        for (var i = 0; i < Items.Count; i++)
        {
            var index = i;
            var item = new ToolStripMenuItem(Items[i].ToString()) { Checked = selected == i, BackColor = Palette.Background, ForeColor = Palette.Text };
            item.Click += (_, _) => { SelectedIndex = index; Focus(); }; menu.Items.Add(item);
        }
        menu.Closed += (_, _) => { if (IsHandleCreated && !IsDisposed) BeginInvoke(() => menu.Dispose()); else menu.Dispose(); };
        menu.Show(this, new Point(0, Height));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; ModernShape.Prepare(g); g.Clear(BackColor);
        using var shape = ModernShape.Round(new RectangleF(1, 1, Width - 2, Height - 2), 8);
        using var fill = new SolidBrush(Palette.Card); using var border = new Pen(Focused ? Palette.Mint : Palette.Border);
        g.FillPath(fill, shape); g.DrawPath(border, shape);
        using var brush = new SolidBrush(Palette.Text); using var font = Palette.Font(9);
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
        g.DrawString(SelectedItem?.ToString() ?? "", font, brush, new RectangleF(11, 0, Width - 38, Height), format);
        g.FillPolygon(brush, [new PointF(Width - 22, Height / 2f - 2), new PointF(Width - 14, Height / 2f - 2), new PointF(Width - 18, Height / 2f + 2)]);
    }
}

internal static class WindowChrome
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    public static void Apply(Form form)
    {
        var round = 2; DwmSetWindowAttribute(form.Handle, 33, ref round, sizeof(int));
        var dark = Palette.Light ? 0 : 1; DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
        var margins = new Margins { Left = 1, Right = 1, Top = 1, Bottom = 1 }; DwmExtendFrameIntoClientArea(form.Handle, ref margins);
        if (Environment.OSVersion.Version.Build < 22000) { using var path = ModernShape.Round(form.ClientRectangle, 18); form.Region = new Region(path); }
    }
    public static void Drag(Form form) { ReleaseCapture(); SendMessage(form.Handle, 0xA1, new IntPtr(2), IntPtr.Zero); }
}

internal sealed class MenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Palette.Background;
    public override Color ImageMarginGradientBegin => Palette.Background;
    public override Color ImageMarginGradientMiddle => Palette.Background;
    public override Color ImageMarginGradientEnd => Palette.Background;
    public override Color MenuBorder => Palette.Border;
    public override Color MenuItemSelected => Palette.Chip;
    public override Color MenuItemBorder => Palette.Border;
    public override Color CheckBackground => Palette.Chip;
    public override Color CheckSelectedBackground => Palette.Chip;
    public override Color SeparatorDark => Palette.Border;
    public override Color SeparatorLight => Palette.Background;
}
