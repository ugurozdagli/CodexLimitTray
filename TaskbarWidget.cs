using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace CodexLimitTray;

// A separate, per-pixel transparent window: no injection into Explorer or shell modifications.
internal sealed class TaskbarWidget : Form
{
    internal static readonly Size LogicalSize = new(62, 32);
    internal static readonly Size DetailedSize = new(182, 32);
    private readonly Settings settings;
    private readonly Action click, save;
    private readonly System.Windows.Forms.Timer positionTimer = new() { Interval = 1000 };
    private readonly ToolTip tooltip = new();
    private UsageSnapshot? snapshot;
    private bool fresh, dragging, moved;
    private Point dragStart;
    private int startGap, anchor;
    private float scale = 1;
    private bool rendered;
    private Native.WinEventProc? windowEvent;
    private IntPtr foregroundHook, reorderHook;
    private bool updateQueued;
    private IntPtr ownerTaskbar;
    internal bool PositionLocked => settings.WidgetLocked;
    internal bool LayerOrderReady => IsHandleCreated && ownerTaskbar != IntPtr.Zero && Native.GetWindow(Handle, 4) == ownerTaskbar && foregroundHook != IntPtr.Zero && reorderHook != IntPtr.Zero;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x8000000 | 0x80; return cp; }
    }
    public TaskbarWidget(Settings settings, ContextMenuStrip menu, Action click, Action save)
    {
        this.settings = settings; this.click = click; this.save = save;
        Text = L10n.T("CLT · Görev çubuğu göstergesi");
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.Manual;
        ClientSize = LogicalSize; ContextMenuStrip = menu;
        MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true; moved = false; dragStart = Cursor.Position;
            startGap = settings.WidgetGap; Capture = true;
        };
        MouseMove += (_, _) =>
        {
            if (!dragging || settings.WidgetLocked) return;
            var dx = Cursor.Position.X - dragStart.X;
            if (Math.Abs(dx) >= 4) moved = true;
            if (moved) { settings.WidgetGap = startGap - dx; UpdatePosition(); }
        };
        MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || !dragging) return;
            dragging = false; Capture = false;
            if (moved) save(); else click();
        };
        positionTimer.Tick += (_, _) => UpdatePosition(); positionTimer.Start();
        UpdatePosition();
        windowEvent = (_, eventType, hwnd, objectId, childId, _, _) =>
        {
            if (IsDisposed || !IsHandleCreated || updateQueued) return;
            if (eventType != 3 && (hwnd != ownerTaskbar || objectId != 0 || childId != 0)) return;
            updateQueued = true;
            try { BeginInvoke(() => { updateQueued = false; if (!IsDisposed) UpdatePosition(restack: true); }); }
            catch (InvalidOperationException) { updateQueued = false; }
        };
        foregroundHook = Native.SetWinEventHook(3, 3, IntPtr.Zero, windowEvent, 0, 0, 0);
        reorderHook = Native.SetWinEventHook(0x8004, 0x8004, IntPtr.Zero, windowEvent, 0, 0, 0);
    }
    public void SetState(UsageSnapshot? data, bool isFresh)
    {
        snapshot = data; fresh = isFresh;
        tooltip.SetToolTip(this, !fresh || data is null ? L10n.T("CLT · Güncel kullanım verisi bekleniyor") :
            L10n.Text("CLT · Kalan kullanım", "CLT · Remaining usage") + "\n" +
            string.Join("\n", VisibleWindows(data).OfType<UsageWindow>().Select(w => $"{w.Duration}: {L10n.Percent(w.Remaining)}")) + "\n" +
            L10n.Text("Sol tık: ayrıntılar · Sağ tık: ayarlar", "Left click: details · Right click: settings"));
        UpdatePosition(); Render();
    }
    public void ResetPosition() { settings.WidgetGap = 8; save(); UpdatePosition(); }
    private void UpdatePosition(bool restack = false)
    {
        if (!settings.WidgetEnabled) { Hide(); return; }
        var info = GetTaskbarInfo(settings.DisplayKey);
        if (info is null) { Hide(); return; }
        var (taskbar, trayLeft, dpi, taskbarHandle) = info.Value;
        ownerTaskbar = taskbarHandle;
        if (Native.IsFullScreen(taskbar)) { Hide(); return; }
        var nextScale = dpi / 96f * settings.TextScale;
        var resized = Math.Abs(scale - nextScale) > .01f;
        scale = nextScale;
        var measured = MeasureWidget(snapshot, fresh, scale, settings.WidgetDetailed, settings.ResetDisplay);
        var width = measured.Width;
        var height = Math.Min(taskbar.Height, measured.Height);
        anchor = trayLeft;
        var x = Math.Clamp(anchor - width - settings.WidgetGap, taskbar.Left, Math.Max(taskbar.Left, taskbar.Right - width));
        var y = taskbar.Top + (taskbar.Height - height) / 2;
        var bounds = new Rectangle(x, y, width, height);
        var sizeChanged = Size != bounds.Size;
        var moved = Bounds != bounds;
        if (moved) Bounds = bounds;
        if (!Visible) { Show(); rendered = false; restack = true; }
        // Owned windows stay above their owner when Explorer raises the taskbar.
        // Only CLT's own window is changed; Explorer's styles and code are untouched.
        if (taskbarHandle != IntPtr.Zero && Native.GetWindow(Handle, 4) != taskbarHandle)
        {
            Native.SetWindowLongPtr(Handle, -8, taskbarHandle);
            restack = true;
        }
        if (restack || moved || sizeChanged)
            Native.SetWindowPos(Handle, new IntPtr(-1), x, y, width, height, 0x10 | 0x40);
        if (resized || sizeChanged || !rendered) Render();
    }
    internal static (Rectangle Taskbar, int Anchor, uint Dpi, IntPtr Handle)? GetTaskbarInfo(string? displayKey = null)
    {
        var handle = Native.FindWindow("Shell_TrayWnd", null);
        var selected = DisplayCatalog.Resolve(displayKey);
        Native.EnumProc findTaskbar = (window, _) =>
        {
            var name = new StringBuilder(128); Native.GetClassName(window, name, name.Capacity);
            if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" && Native.GetWindowRect(window, out var bounds) &&
                Screen.FromRectangle(bounds.Rectangle).DeviceName == selected) handle = window;
            return true;
        };
        Native.EnumWindows(findTaskbar, IntPtr.Zero);
        if (handle == IntPtr.Zero || !Native.IsWindowVisible(handle) || !Native.GetWindowRect(handle, out var r)) return null;
        var taskbar = r.Rectangle;
        if (taskbar.Width < taskbar.Height || taskbar.Height < 8) return null;
        var monitor = Screen.FromRectangle(taskbar).Bounds;
        if (!monitor.IntersectsWith(taskbar) || taskbar.Top >= monitor.Bottom - 2) return null;
        var tray = Native.FindWindowEx(handle, IntPtr.Zero, "TrayNotifyWnd", null);
        var anchor = tray != IntPtr.Zero && Native.GetWindowRect(tray, out var tr) ? tr.Left : taskbar.Right - (Screen.FromRectangle(taskbar).Primary ? 340 : 120);
        // Place CLT before an existing NetSpeedTray widget instead of covering it.
        var netPids = Process.GetProcessesByName("NetSpeedTray").Select(p => { using (p) return (uint)p.Id; }).ToHashSet();
        if (netPids.Count > 0)
        {
            Native.EnumProc inspect = (window, _) =>
            {
                Native.GetWindowThreadProcessId(window, out var pid);
                if (netPids.Contains(pid) && Native.IsWindowVisible(window) && Native.GetWindowRect(window, out var nr))
                {
                    var rect = nr.Rectangle;
                    if (rect.Width is >= 30 and <= 500 && rect.Height <= taskbar.Height + 12 && rect.IntersectsWith(taskbar)) anchor = Math.Min(anchor, rect.Left);
                }
                return true;
            };
            Native.EnumWindows(inspect, IntPtr.Zero);
            Native.EnumChildWindows(handle, inspect, IntPtr.Zero);
        }
        var dpi = Native.GetDpiForWindow(handle);
        return (taskbar, anchor, dpi is >= 96 and <= 480 ? dpi : 96, handle);
    }
    internal static UsageWindow?[] VisibleWindows(UsageSnapshot? data)
    {
        if (data is null) return [null, null];
        return new[] { data.Main.Primary, data.Main.Secondary }.Where(w => w is not null).ToArray();
    }
    private static string ResetLabel(UsageWindow? window, bool fresh, ResetDisplay display)
        => !fresh || window is null ? "—" : display == ResetDisplay.Countdown ? window.CompactCountdown(DateTimeOffset.Now) : window.ResetText;
    private static (float Label, float Value, float Reset) Columns(Graphics g, UsageSnapshot? data, bool fresh, float scale, bool detailed, ResetDisplay reset)
    {
        using var font = new Font("Segoe UI", 12 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var valueFont = new Font("Segoe UI", 12 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        using var dateFont = new Font("Segoe UI", 11 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        var windows = VisibleWindows(data);
        var labels = windows.Select(w => detailed ? w?.Duration ?? L10n.T("Limit") : w?.CompactDuration ?? "—");
        float Width(string text, Font f) => (float)Math.Ceiling(g.MeasureString(text, f, 1000, StringFormat.GenericTypographic).Width);
        var label = Math.Max((detailed ? 44 : 16) * scale, labels.Max(t => Width(t, font)));
        var value = Math.Max(30 * scale, Width(L10n.Percent(100), valueFont));
        var date = detailed && reset != ResetDisplay.Hidden ? Math.Max(36 * scale, windows.Max(w => Width(ResetLabel(w, fresh, reset), dateFont))) : 0;
        return (label, value, date);
    }
    internal static Size MeasureWidget(UsageSnapshot? data, bool fresh, float scale = 1, bool detailed = false, ResetDisplay reset = ResetDisplay.Date)
    {
        using var bitmap = new Bitmap(1, 1); using var g = Graphics.FromImage(bitmap);
        var c = Columns(g, data, fresh, scale, detailed, reset);
        return new Size((int)Math.Ceiling(c.Label + c.Value + 12 * scale + (c.Reset > 0 ? c.Reset + 14 * scale : 0)), (int)Math.Ceiling(16 * scale * VisibleWindows(data).Length));
    }
    internal static Bitmap DrawWidget(UsageSnapshot? data, bool fresh, Size size, float scale = 1, bool detailed = false, ResetDisplay resetDisplay = ResetDisplay.Date, bool? lightTaskbar = null)
    {
        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        var light = lightTaskbar ?? Palette.WindowsLight(taskbar: true);
        using var font = new Font("Segoe UI", 12 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var valueFont = new Font("Segoe UI", 12 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        using var dateFont = new Font("Segoe UI", 11 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center };
        using var labelBrush = new SolidBrush(light ? Color.FromArgb(49, 65, 83) : Color.FromArgb(175, 190, 208));
        var columns = Columns(g, data, fresh, scale, detailed, resetDisplay);
        var windows = VisibleWindows(data);
        for (var i = 0; i < windows.Length; i++)
        {
            var window = windows[i];
            var label = detailed ? window?.Duration ?? L10n.T("Limit") : window?.CompactDuration ?? "—";
            var value = fresh && window is not null ? L10n.Percent(window.Remaining) : "—";
            var row = new RectangleF(3 * scale, i * size.Height / (float)windows.Length, columns.Label, size.Height / (float)windows.Length);
            g.DrawString(label, font, labelBrush, row, format);
            using var valueBrush = new SolidBrush(fresh && window is not null ? Palette.For(window.Remaining, light) : labelBrush.Color);
            var valueRow = new RectangleF(row.Right + 5 * scale, row.Y, columns.Value + scale, row.Height);
            g.DrawString(value, valueFont, valueBrush, valueRow, format);
            if (columns.Reset > 0)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var dotX = valueRow.Right + 5 * scale;
                g.FillEllipse(labelBrush, dotX, row.Y + (row.Height - 2.6f * scale) / 2, 2.6f * scale, 2.6f * scale);
                g.DrawString(ResetLabel(window, fresh, resetDisplay), dateFont, labelBrush, new RectangleF(dotX + 7 * scale, row.Y, columns.Reset + 2 * scale, row.Height), format);
            }
        }
        ApplyContentHitArea(bitmap);
        return bitmap;
    }
    private static void ApplyContentHitArea(Bitmap bitmap)
    {
        var left = bitmap.Width; var top = bitmap.Height; var right = -1; var bottom = -1;
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).A > 16)
                {
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                }
        if (right < left) return;
        // One-pixel tolerance around the visible text, with draggable gaps inside it.
        left = Math.Max(1, left - 1); right = Math.Min(bitmap.Width - 2, right + 1);
        top = Math.Max(1, top - 1); bottom = Math.Min(bitmap.Height - 2, bottom + 1);
        for (var y = top; y <= bottom; y++)
            for (var x = left; x <= right; x++)
                if (bitmap.GetPixel(x, y).A == 0) bitmap.SetPixel(x, y, Color.FromArgb(1, 0, 0, 0));
    }
    private void Render()
    {
        if (!IsHandleCreated || Width < 1 || Height < 1) return;
        using var bitmap = DrawWidget(snapshot, fresh, ClientSize, scale, settings.WidgetDetailed, settings.ResetDisplay);
        Native.ApplyBitmap(Handle, bitmap, Location); rendered = true;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (foregroundHook != IntPtr.Zero) { Native.UnhookWinEvent(foregroundHook); foregroundHook = IntPtr.Zero; }
            if (reorderHook != IntPtr.Zero) { Native.UnhookWinEvent(reorderHook); reorderHook = IntPtr.Zero; }
            positionTimer.Stop(); positionTimer.Dispose(); tooltip.Dispose();
        }
        base.Dispose(disposing);
    }

    internal static bool CoversScreen(Rectangle window, Rectangle screen, bool maximized, bool shellSurface)
        => !maximized && !shellSurface && Math.Abs(window.Left - screen.Left) <= 1 && Math.Abs(window.Top - screen.Top) <= 1 && Math.Abs(window.Right - screen.Right) <= 1 && Math.Abs(window.Bottom - screen.Bottom) <= 1;

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; public Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom); }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int Width, Height; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BLEND { public byte Op, Flags, Alpha, Format; }
        internal delegate bool EnumProc(IntPtr handle, IntPtr param);
        internal delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int objectId, int childId, uint thread, uint time);
        [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] internal static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT rect, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls, string? title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? title);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out RECT rect);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc proc, IntPtr param);
        [DllImport("user32.dll")] internal static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr param);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr window, StringBuilder text, int max);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dstDc, ref POINT dst, ref SIZE size, IntPtr srcDc, ref POINT src, uint key, ref BLEND blend, uint flags);
        internal static bool IsFullScreen(Rectangle taskbar)
        {
            var foreground = GetForegroundWindow();
            var name = new StringBuilder(128); GetClassName(foreground, name, name.Capacity);
            var shell = name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "MultitaskingViewFrame" or "TaskSwitcherWnd" or "XamlExplorerHostIslandWindow" or "Windows.UI.Core.CoreWindow";
            if (shell || IsZoomed(foreground) || IsIconic(foreground)) return false;
            if (!GetWindowRect(foreground, out var rect)) return false;
            if (DwmGetWindowAttribute(foreground, 9, out var visible, Marshal.SizeOf<RECT>()) == 0) rect = visible;
            var screen = Screen.FromRectangle(taskbar).Bounds;
            return CoversScreen(rect.Rectangle, screen, false, false);
        }
        internal static void ApplyBitmap(IntPtr hwnd, Bitmap bitmap, Point location)
        {
            var screen = GetDC(IntPtr.Zero); var memory = CreateCompatibleDC(screen);
            var handle = bitmap.GetHbitmap(Color.FromArgb(0)); var old = SelectObject(memory, handle);
            try
            {
                var dst = new POINT(location.X, location.Y); var src = new POINT(0, 0);
                var size = new SIZE { Width = bitmap.Width, Height = bitmap.Height };
                var blend = new BLEND { Alpha = 255, Format = 1 };
                if (!UpdateLayeredWindow(hwnd, screen, ref dst, ref size, memory, ref src, 0, ref blend, 2)) throw new System.ComponentModel.Win32Exception();
            }
            finally { SelectObject(memory, old); DeleteObject(handle); DeleteDC(memory); ReleaseDC(IntPtr.Zero, screen); }
        }
    }
}
