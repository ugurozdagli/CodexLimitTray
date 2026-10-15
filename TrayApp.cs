using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace CodexLimitTray;

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
    public static bool Light { get; private set; }
    public static Color Background => Light ? Color.FromArgb(244, 247, 251) : Color.FromArgb(17, 22, 30);
    public static Color Card => Light ? Color.White : Color.FromArgb(29, 39, 53);
    public static Color CardBottom => Light ? Color.FromArgb(237, 242, 248) : Color.FromArgb(24, 32, 44);
    public static Color Text => Light ? Color.FromArgb(24, 34, 46) : Color.FromArgb(237, 242, 247);
    public static Color Muted => Light ? Color.FromArgb(77, 94, 115) : Color.FromArgb(149, 163, 181);
    public static Color Mint => Light ? Color.FromArgb(12, 126, 95) : Color.FromArgb(95, 231, 185);
    public static Color Border => Light ? Color.FromArgb(194, 205, 218) : Color.FromArgb(44, 59, 77);
    public static Color Track => Light ? Color.FromArgb(210, 220, 232) : Color.FromArgb(48, 62, 80);
    public static Color Chip => Light ? Color.FromArgb(224, 233, 240) : Color.FromArgb(30, 41, 55);
    public static Color PrimaryText => Light ? Color.White : Color.FromArgb(15, 44, 35);
    public static bool WindowsLight(bool taskbar = false)
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return key?.GetValue(taskbar ? "SystemUsesLightTheme" : "AppsUseLightTheme") is int value && value != 0; }
        catch { return false; }
    }
    public static void Apply(ThemeMode theme) => Light = theme == ThemeMode.Light || theme == ThemeMode.System && WindowsLight();
    public static Color For(int remaining, bool? light = null) => (light ?? Light)
        ? remaining <= 15 ? Color.FromArgb(188, 36, 62) : remaining <= 35 ? Color.FromArgb(145, 90, 0) : Color.FromArgb(12, 126, 95)
        : remaining <= 15 ? Color.FromArgb(255, 113, 125) : remaining <= 35 ? Color.FromArgb(255, 200, 98) : Color.FromArgb(95, 231, 185);
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
        using var brush = new SolidBrush(remaining is { } n ? Palette.For(n, Palette.WindowsLight(taskbar: true)) : Palette.Muted);
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
    private readonly ToolStripMenuItem shortMode = new(L10n.T("Kısa dönem kalan %")), weeklyMode = new(L10n.T("Haftalık kalan %")), startupItem = new(L10n.T("Windows ile başlat"));
    private readonly QuotaAlerts alerts = new();
    private SettingsForm? settingsDialog;
    private bool checkingUpdates;
    private DateTimeOffset lastAttempt = DateTimeOffset.MinValue;
    private int failures;
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
        Palette.Apply(settings.Theme);
        _ = dispatcher.Handle;
        wait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => { try { dispatcher.BeginInvoke(() => ShowPopup()); } catch (InvalidOperationException) { } }, null, Timeout.Infinite, false);
        menu.Items.Add("CLT · v" + AppInfo.Version, null, (_, _) => ShowPopup());
        menu.Items.Add(L10n.Text("Ayarlar…", "Settings…"), null, (_, _) => ShowSettings());
        menu.Items.Add(L10n.Text("Güncellemeleri kontrol et", "Check for updates"), null, async (_, _) => await CheckUpdatesAsync());
        menu.Items.Add(L10n.T("Şimdi yenile"), null, async (_, _) => await RefreshAsync());
        var widgetItem = new ToolStripMenuItem(L10n.T("Görev çubuğunda iki satır"));
        var lockItem = new ToolStripMenuItem(L10n.T("Gösterge konumunu kilitle"));
        widgetItem.Click += (_, _) => { settings.WidgetEnabled = !settings.WidgetEnabled; SaveSettings(); widget?.SetState(snapshot, Fresh); };
        lockItem.Click += (_, _) => { settings.WidgetLocked = !settings.WidgetLocked; SaveSettings(); };
        menu.Items.Add(widgetItem); menu.Items.Add(lockItem);
        var appearance = new ToolStripMenuItem(L10n.T("Görünüm"));
        var simpleView = new ToolStripMenuItem(L10n.T("Basit"));
        var detailedView = new ToolStripMenuItem(L10n.T("Detaylı"));
        simpleView.Click += (_, _) => { settings.WidgetDetailed = false; SaveSettings(); UpdateTray(); };
        detailedView.Click += (_, _) => { settings.WidgetDetailed = true; SaveSettings(); UpdateTray(); };
        appearance.DropDownItems.AddRange([simpleView, detailedView]);
        menu.Items.Add(appearance);
        menu.Items.Add(L10n.T("Gösterge konumunu sıfırla"), null, (_, _) => widget?.ResetPosition());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(shortMode); menu.Items.Add(weeklyMode);
        shortMode.Click += (_, _) => ChangeMode(false);
        weeklyMode.Click += (_, _) => ChangeMode(true);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startupItem);
        startupItem.Click += (_, _) => ToggleStartup();
        menu.Items.Add(L10n.T("Tray görünürlük ayarları"), null, (_, _) => OpenTraySettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L10n.T("Çıkış"), null, (_, _) => ExitThread());
        menu.Opening += (_, _) => { startupItem.Checked = Startup.Enabled; shortMode.Checked = !settings.Weekly; weeklyMode.Checked = settings.Weekly; widgetItem.Checked = settings.WidgetEnabled; lockItem.Checked = settings.WidgetLocked; simpleView.Checked = !settings.WidgetDetailed; detailedView.Checked = settings.WidgetDetailed; };
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) { if (popup?.Visible == true) popup.Hide(); else ShowPopup(); } };
        widget = new TaskbarWidget(settings, menu, () => ShowPopup(), SaveSettings);
        UpdateTray(); tray.Visible = true;
        poll.Tick += async (_, _) => { await RefreshAsync(); };
        clock.Tick += (_, _) => { UpdateTray(); UpdatePopup(); };
        ApplyMenuColors();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        poll.Start(); clock.Start();
    }

    private void ShowSettings()
    {
        popup?.Hide();
        if (settingsDialog is { IsDisposed: false }) { settingsDialog.Activate(); return; }
        settingsDialog = new SettingsForm(settings, () =>
        {
            SaveSettings(); alerts.Reset(); ApplyAppearance(); UpdateTray();
        }, () => _ = CheckUpdatesAsync(), snapshot?.AccountKey is not null);
        settingsDialog.Show();
    }
    private void ApplyAppearance()
    {
        Palette.Apply(settings.Theme);
        popup?.Dispose(); popup = null;
        ApplyMenuColors();
        widget?.SetState(snapshot, Fresh);
    }
    private void ApplyMenuColors()
    {
        menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors());
        void PaintItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                item.ForeColor = Palette.Text; item.BackColor = Palette.Background;
                if (item is ToolStripMenuItem sub) PaintItems(sub.DropDownItems);
            }
        }
        PaintItems(menu.Items);
    }
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => QueueUi(() => { ApplyAppearance(); UpdateTray(); });
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) QueueUi(() => { alerts.Reset(); _ = RefreshAsync(afterResume: true); });
    }
    private void QueueUi(Action action)
    {
        try { if (!exiting && !dispatcher.IsDisposed) dispatcher.BeginInvoke(() => { if (!exiting) action(); }); }
        catch (InvalidOperationException) { }
    }
    private async Task CheckUpdatesAsync()
    {
        if (checkingUpdates || exiting) return;
        checkingUpdates = true;
        try
        {
            var version = await UpdateChecker.CheckAsync(shutdown.Token);
            if (exiting) return;
            var owner = UpdateOwner;
            if (version is null)
                MessageBox.Show(owner, L10n.Text("v{0} güncel.", "v{0} is up to date.", AppInfo.Version), "CodexLimitTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else if (MessageBox.Show(owner, L10n.Text("v{0} hazır. İndirme sayfası açılsın mı?", "v{0} is available. Open the download page?", version), "CodexLimitTray", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(AppInfo.ReleasesUrl + "/tag/v" + version) { UseShellExecute = true });
        }
        catch (OperationCanceledException) when (exiting) { }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or JsonException or IOException or System.ComponentModel.Win32Exception)
        {
            if (!exiting) MessageBox.Show(UpdateOwner, L10n.Text("Güncelleme kontrol edilemedi. Bağlantını kontrol edip tekrar dene.", "Could not check for updates. Check your connection and try again."), "CodexLimitTray", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { checkingUpdates = false; }
    }
    private IWin32Window UpdateOwner => settingsDialog is { IsDisposed: false } ? settingsDialog : dispatcher;

    private void ChangeMode(bool weekly)
    {
        settings.Weekly = weekly;
        SaveSettings();
        UpdateTray(); UpdatePopup();
    }
    private void SaveSettings()
    {
        try { settings.Save(); } catch { tray.ShowBalloonTip(4000, "CLT", L10n.T("Görünüm tercihi kaydedilemedi."), ToolTipIcon.Warning); }
    }
    private void ToggleStartup()
    {
        try { Startup.Set(!Startup.Enabled); }
        catch { MessageBox.Show(L10n.T("Windows başlangıç ayarı kaydedilemedi."), "CodexLimitTray", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        UpdatePopup();
    }
    internal static void OpenTraySettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:taskbar") { UseShellExecute = true }); }
        catch { MessageBox.Show(L10n.T("Görev çubuğuna sağ tıkla → Görev çubuğu ayarları → Diğer sistem tepsisi simgeleri."), "CLT"); }
    }
    private async Task RefreshAsync(bool afterResume = false)
    {
        if (busy || exiting || !afterResume && DateTimeOffset.Now - lastAttempt < TimeSpan.FromSeconds(10)) return;
        lastAttempt = DateTimeOffset.Now;
        busy = true; UpdatePopup();
        try
        {
            snapshot = await CodexClient.FetchAsync(shutdown.Token); error = null; failures = 0; poll.Interval = 60000;
            var notices = alerts.Observe(snapshot, settings);
            if (notices.Count > 0)
            {
                var body = string.Join("\n", notices.Select(n => n.Replenished
                    ? L10n.Text("{0}: kota yenilendi · {1}", "{0}: quota replenished · {1}", n.Window, L10n.Percent(n.Remaining))
                    : L10n.Text("{0}: {1} kaldı", "{0}: {1} remaining", n.Window, L10n.Percent(n.Remaining))));
                tray.ShowBalloonTip(5000, "CodexLimitTray", body.Length > 255 ? body[..252] + "…" : body, notices.Any(n => n.Critical) ? ToolTipIcon.Warning : ToolTipIcon.Info);
            }
        }
        catch (OperationCanceledException) when (exiting) { }
        catch (Exception ex) { alerts.Reset(); poll.Interval = Math.Min(300000, 60000 * (1 << Math.Min(++failures, 3))); error = ex is TimeoutException or InvalidOperationException or FileNotFoundException or IOException ? ex.Message : L10n.T("Kullanım verisi alınamadı. Codex oturumunu kontrol edip tekrar dene."); }
        finally { busy = false; if (!exiting) { UpdateTray(); UpdatePopup(); } }
    }
    private bool Fresh => error is null && snapshot is not null && DateTimeOffset.Now - snapshot.FetchedAt < TimeSpan.FromMinutes(3);
    private void UpdateTray()
    {
        shortMode.Text = (snapshot?.Main.Primary?.Duration ?? L10n.T("Kısa")) + " · " + L10n.T("kalan") + " %";
        weeklyMode.Text = (snapshot?.Main.Secondary?.Duration ?? L10n.T("Haftalık")) + " · " + L10n.T("kalan") + " %";
        var window = snapshot is null ? null : settings.Weekly ? snapshot.Main.Secondary ?? snapshot.Main.Primary : snapshot.Main.Primary ?? snapshot.Main.Secondary;
        var icon = TrayDrawing.Create(Fresh ? window?.Remaining : null);
        tray.Icon = icon;
        currentIcon?.Dispose(); currentIcon = icon;
        var lines = new List<string> { L10n.T("CLT · Codex kalan kullanım") };
        if (!Fresh) lines.Add(error is null ? L10n.T("Veri bekleniyor…") : L10n.T("Güncel veri alınamadı; tıkla"));
        else
        {
            foreach (var w in new[] { snapshot!.Main.Primary, snapshot.Main.Secondary }.OfType<UsageWindow>())
                lines.Add($"{w.Duration}: {L10n.Percent(w.Remaining)} · {w.ResetText}");
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
            popup = new Popup(() => _ = RefreshAsync(), ToggleStartup, () => ChangeMode(!settings.Weekly), ShowSettings);
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
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        settingsDialog?.Dispose();
        wait.Unregister(null); tray.Visible = false; tray.Dispose(); currentIcon?.Dispose();
        popup?.Dispose(); widget?.Dispose(); dispatcher.Dispose(); menu.Dispose(); poll.Dispose(); clock.Dispose();
        base.ExitThreadCore();
    }
}
