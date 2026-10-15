using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace CodexLimitTray;

internal sealed class Settings
{
    public bool Weekly { get; set; }
    public bool WidgetEnabled { get; set; } = true;
    public bool WidgetDetailed { get; set; }
    public bool WidgetLocked { get; set; }
    public int WidgetGap { get; set; } = 8;
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexLimitTray", "settings.json");
    public static Settings Load() { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); } }
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); }
}

internal static class Startup
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue("CodexLimitTray") is string; } }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("CodexLimitTray", $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue("CodexLimitTray", false);
    }
}

internal static class Palette
{
    public static readonly Color Background = Color.FromArgb(17, 22, 30), Card = Color.FromArgb(27, 34, 45), Text = Color.FromArgb(237, 242, 247), Muted = Color.FromArgb(149, 163, 181), Mint = Color.FromArgb(95, 231, 185);
    public static Color For(int remaining) => remaining <= 15 ? Color.FromArgb(255, 113, 125) : remaining <= 35 ? Color.FromArgb(255, 200, 98) : Mint;
    public static Font Font(float size, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style, GraphicsUnit.Point);
}

internal static class TrayDrawing
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    public static Icon Create(int? remaining)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Segoe UI", remaining == 100 ? 17 : 22, FontStyle.Bold, GraphicsUnit.Pixel);
        var text = remaining?.ToString() ?? "–";
        using var brush = new SolidBrush(remaining is { } n ? Palette.For(n) : Palette.Muted);
        using var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(text, font, brush, new RectangleF(0, 0, 32, 32), format);
        var handle = bmp.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); }
        finally { DestroyIcon(handle); }
    }
}

internal sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon tray = new();
    private readonly System.Windows.Forms.Timer poll = new() { Interval = 100 };
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 30000 };
    private readonly Control dispatcher = new();
    private readonly CancellationTokenSource shutdown = new();
    private readonly Settings settings = Settings.Load();
    private readonly ContextMenuStrip menu = new();
    private readonly ToolStripMenuItem shortMode = new("Kısa dönem kalan %"), weeklyMode = new("Haftalık kalan %"), startupItem = new("Windows ile başlat");
    private UsageSnapshot? snapshot;
    private string? error;
    private bool busy, exiting;
    private Popup? popup;
    private Icon? currentIcon;
    private TaskbarWidget? widget;
    private readonly EventWaitHandle showEvent;
    private readonly RegisteredWaitHandle wait;
    internal bool ReadyForCheck => Fresh && tray.Visible && !busy && (!settings.WidgetEnabled || widget?.LayerOrderReady == true);
    internal bool FailedForCheck => error is not null && tray.Visible && !busy;

    public TrayApp(EventWaitHandle showEvent)
    {
        this.showEvent = showEvent;
        _ = dispatcher.Handle;
        wait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => { try { dispatcher.BeginInvoke(() => ShowPopup()); } catch (InvalidOperationException) { } }, null, Timeout.Infinite, false);
        menu.Items.Add("CLT · CodexLimitTray", null, (_, _) => ShowPopup());
        menu.Items.Add("Şimdi yenile", null, async (_, _) => await RefreshAsync());
        var widgetItem = new ToolStripMenuItem("Görev çubuğunda iki satır");
        var lockItem = new ToolStripMenuItem("Gösterge konumunu kilitle");
        widgetItem.Click += (_, _) => { settings.WidgetEnabled = !settings.WidgetEnabled; SaveSettings(); widget?.SetState(snapshot, Fresh); };
        lockItem.Click += (_, _) => { settings.WidgetLocked = !settings.WidgetLocked; SaveSettings(); };
        menu.Items.Add(widgetItem); menu.Items.Add(lockItem);
        var appearance = new ToolStripMenuItem("Görünüm");
        var simpleView = new ToolStripMenuItem("Basit");
        var detailedView = new ToolStripMenuItem("Detaylı");
        simpleView.Click += (_, _) => { settings.WidgetDetailed = false; SaveSettings(); UpdateTray(); };
        detailedView.Click += (_, _) => { settings.WidgetDetailed = true; SaveSettings(); UpdateTray(); };
        appearance.DropDownItems.AddRange([simpleView, detailedView]);
        menu.Items.Add(appearance);
        menu.Items.Add("Gösterge konumunu sıfırla", null, (_, _) => widget?.ResetPosition());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(shortMode); menu.Items.Add(weeklyMode);
        shortMode.Click += (_, _) => ChangeMode(false);
        weeklyMode.Click += (_, _) => ChangeMode(true);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startupItem);
        startupItem.Click += (_, _) => ToggleStartup();
        menu.Items.Add("Tray görünürlük ayarları", null, (_, _) => OpenTraySettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => ExitThread());
        menu.Opening += (_, _) => { startupItem.Checked = Startup.Enabled; shortMode.Checked = !settings.Weekly; weeklyMode.Checked = settings.Weekly; widgetItem.Checked = settings.WidgetEnabled; lockItem.Checked = settings.WidgetLocked; simpleView.Checked = !settings.WidgetDetailed; detailedView.Checked = settings.WidgetDetailed; };
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) { if (popup?.Visible == true) popup.Hide(); else ShowPopup(); } };
        widget = new TaskbarWidget(settings, menu, () => ShowPopup(), SaveSettings);
        UpdateTray(); tray.Visible = true;
        poll.Tick += async (_, _) => { poll.Interval = 60000; await RefreshAsync(); };
        clock.Tick += (_, _) => { UpdateTray(); UpdatePopup(); };
        poll.Start(); clock.Start();
    }

    private void ChangeMode(bool weekly)
    {
        settings.Weekly = weekly;
        SaveSettings();
        UpdateTray(); UpdatePopup();
    }
    private void SaveSettings()
    {
        try { settings.Save(); } catch { tray.ShowBalloonTip(4000, "CLT", "Görünüm tercihi kaydedilemedi.", ToolTipIcon.Warning); }
    }
    private void ToggleStartup()
    {
        try { Startup.Set(!Startup.Enabled); }
        catch { MessageBox.Show("Windows başlangıç ayarı kaydedilemedi.", "CodexLimitTray", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        UpdatePopup();
    }
    internal static void OpenTraySettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:taskbar") { UseShellExecute = true }); }
        catch { MessageBox.Show("Görev çubuğuna sağ tıkla → Görev çubuğu ayarları → Diğer sistem tepsisi simgeleri.", "CLT"); }
    }
    private async Task RefreshAsync()
    {
        if (busy || exiting) return;
        busy = true; UpdatePopup();
        try { snapshot = await CodexClient.FetchAsync(shutdown.Token); error = null; }
        catch (OperationCanceledException) when (exiting) { }
        catch (Exception ex) { error = ex is TimeoutException or InvalidOperationException or FileNotFoundException or IOException ? ex.Message : "Kullanım verisi alınamadı. Codex oturumunu kontrol edip tekrar dene."; }
        finally { busy = false; if (!exiting) { UpdateTray(); UpdatePopup(); } }
    }
    private bool Fresh => error is null && snapshot is not null && DateTimeOffset.Now - snapshot.FetchedAt < TimeSpan.FromMinutes(3);
    private void UpdateTray()
    {
        var window = snapshot is null ? null : settings.Weekly ? snapshot.Main.Secondary : snapshot.Main.Primary;
        var icon = TrayDrawing.Create(Fresh ? window?.Remaining : null);
        tray.Icon = icon;
        currentIcon?.Dispose(); currentIcon = icon;
        var lines = new List<string> { "CLT · Codex kalan kullanım" };
        if (!Fresh) lines.Add(error is null ? "Veri bekleniyor…" : "Güncel veri alınamadı; tıkla");
        else
        {
            foreach (var w in new[] { snapshot!.Main.Primary, snapshot.Main.Secondary }.OfType<UsageWindow>())
                lines.Add($"{w.Duration}: %{w.Remaining} · {w.ResetText}");
        }
        var text = string.Join('\n', lines);
        tray.Text = text.Length > 127 ? text[..127] : text;
        widget?.SetState(snapshot, Fresh);
    }
    private void ShowPopup()
    {
        if (exiting) return;
        if (popup is null || popup.IsDisposed)
        {
            popup = new Popup(() => _ = RefreshAsync(), ToggleStartup, () => ChangeMode(!settings.Weekly));
            popup.FormClosing += (_, e) => { if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; popup.Hide(); } };
        }
        UpdatePopup();
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        popup.Height = Math.Min(620, screen.Height - 24);
        popup.Location = new Point(Math.Clamp(Cursor.Position.X - popup.Width / 2, screen.Left + 8, Math.Max(screen.Left + 8, screen.Right - popup.Width - 8)), screen.Bottom - popup.Height - 8);
        popup.Show(); popup.Activate();
    }
    private void UpdatePopup() => popup?.SetState(snapshot, error, busy, settings.Weekly, Fresh);
    protected override void ExitThreadCore()
    {
        if (exiting) return;
        exiting = true; poll.Stop(); clock.Stop(); shutdown.Cancel();
        wait.Unregister(null); tray.Visible = false; tray.Dispose(); currentIcon?.Dispose();
        popup?.Dispose(); widget?.Dispose(); dispatcher.Dispose(); menu.Dispose(); poll.Dispose(); clock.Dispose();
        base.ExitThreadCore();
    }
}
