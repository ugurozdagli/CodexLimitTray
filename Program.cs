using System.Text.Json;

namespace CodexLimitTray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length > 0) return Diagnostics.Run(args);
        using var mutex = new Mutex(true, @"Local\CodexLimitTray.Instance.v1", out var first);
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\CodexLimitTray.Show.v1");
        if (!first) { showEvent.Set(); return 0; }
        try { using var app = new TrayApp(showEvent); Application.Run(app); return 0; }
        finally { mutex.ReleaseMutex(); }
    }
}

internal static class Diagnostics
{
    public static int Run(string[] args)
    {
        var output = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(AppContext.BaseDirectory, "diagnostics.json");
        try
        {
            // Diagnostics can render either language without changing Windows settings.
            if (args.Length > 2 && args[2] is "en-US" or "tr-TR")
                System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(args[2]);
            if (args[0] is "--demo-previews" or "--feature-previews")
            {
                Directory.CreateDirectory(output);
                // Public documentation uses fictional data, never a contributor's live account.
                var demo = new UsageSnapshot([new UsageBucket("codex", "Codex", new UsageWindow(28, 300, DateTimeOffset.Now.AddHours(3)), new UsageWindow(14, 10080, DateTimeOffset.Now.AddDays(5)), "plus")], DateTimeOffset.Now);
                if (args[0] == "--feature-previews")
                {
                    foreach (var theme in new[] { ThemeMode.Dark, ThemeMode.Light })
                    {
                        Palette.Apply(theme);
                        using var panel = new Popup(() => { }, () => { }, () => { }, () => { });
                        panel.SetState(demo, null, false, false, true);
                        SaveForm(panel, "panel-" + theme + ".png");
                        using var options = new SettingsForm(new Settings { Theme = theme, LowQuotaAlerts = true }, () => { }, () => { }, previewStartup: false);
                        SaveForm(options, "settings-" + theme + ".png");
                        var size = TaskbarWidget.MeasureWidget(demo, true, 1.2f, true, ResetDisplay.Countdown);
                        using var widget = TaskbarWidget.DrawWidget(demo, true, size, 1.2f, true, ResetDisplay.Countdown, theme == ThemeMode.Light);
                        using var canvas = new Bitmap(size.Width + 40, size.Height + 30);
                        using var g = Graphics.FromImage(canvas); g.Clear(Palette.Background); g.DrawImageUnscaled(widget, 20, 15);
                        canvas.Save(Path.Combine(output, "widget-" + theme + ".png"));
                    }
                    Palette.Apply(ThemeMode.Dark);
                    return 0;
                    void SaveForm(Form form, string file)
                    {
                        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.Show(); Application.DoEvents();
                        using var bitmap = new Bitmap(form.Width, form.Height);
                        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(output, file), System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                using var popup = new Popup(() => { }, () => { }, () => { });
                popup.SetState(demo, null, false, false, true);
                popup.Location = new Point(-30000, -30000); popup.Show(); Application.DoEvents();
                using (var panel = new Bitmap(popup.Width, popup.Height))
                {
                    popup.DrawToBitmap(panel, popup.ClientRectangle);
                    panel.Save(Path.Combine(output, "usage-panel.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
                foreach (var detailed in new[] { false, true })
                {
                    using var widget = TaskbarWidget.DrawWidget(demo, true, detailed ? TaskbarWidget.DetailedSize : TaskbarWidget.LogicalSize, detailed: detailed);
                    using var canvas = new Bitmap(460, 72);
                    using var graphics = Graphics.FromImage(canvas);
                    graphics.Clear(Color.FromArgb(26, 33, 42));
                    graphics.DrawImageUnscaled(widget, 22, 20);
                    ModernShape.Prepare(graphics);
                    using var font = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel);
                    using var brush = new SolidBrush(Palette.Muted);
                    graphics.DrawString(detailed ? "Detailed view · demo data" : "Simple view · demo data", font, brush, 250, 29);
                    canvas.Save(Path.Combine(output, detailed ? "taskbar-detailed.png" : "taskbar-simple.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            else if (args[0] == "--self-test")
            {
                var tests = RunTests();
                File.WriteAllText(output, JsonSerializer.Serialize(new { passed = tests.Count, tests }, new JsonSerializerOptions { WriteIndented = true }));
            }
            else if (args[0] == "--check-update")
            {
                var available = UpdateChecker.CheckAsync(CancellationToken.None).GetAwaiter().GetResult();
                File.WriteAllText(output, JsonSerializer.Serialize(new { currentVersion = AppInfo.Version, availableVersion = available?.ToString() }));
            }
            else if (args[0] is "--smoke-test" or "--smoke-test-error")
            {
                var expectError = args[0] == "--smoke-test-error";
                var previousPath = Environment.GetEnvironmentVariable("CLT_CODEX_PATH");
                if (expectError) Environment.SetEnvironmentVariable("CLT_CODEX_PATH", Path.Combine(Path.GetTempPath(), "clt-missing-codex-test.exe"));
                try
                {
                    using var signal = new EventWaitHandle(false, EventResetMode.AutoReset);
                    using var app = new TrayApp(signal);
                    using var timer = new System.Windows.Forms.Timer { Interval = 100 };
                    var deadline = DateTimeOffset.Now.AddSeconds(45);
                    var passed = false;
                    timer.Tick += (_, _) =>
                    {
                        passed = expectError ? app.FailedForCheck : app.ReadyForCheck;
                        if (passed || DateTimeOffset.Now > deadline) { timer.Stop(); app.ExitThread(); }
                    };
                    timer.Start(); Application.Run(app);
                    if (!passed) throw new Exception("Tray message-loop test timed out.");
                    File.WriteAllText(output, JsonSerializer.Serialize(new { ok = true, test = expectError ? "Missing executable shows unavailable tray state" : "Real usage fetched in tray message loop" }));
                }
                finally { Environment.SetEnvironmentVariable("CLT_CODEX_PATH", previousPath); }
            }
            else if (args[0] == "--taskbar-info")
            {
                var info = TaskbarWidget.GetTaskbarInfo() ?? throw new Exception(L10n.T("Windows görev çubuğu bulunamadı."));
                File.WriteAllText(output, JsonSerializer.Serialize(new { taskbar = info.Taskbar, anchor = info.Anchor, dpi = info.Dpi }, new JsonSerializerOptions { WriteIndented = true }));
            }
            else if (args[0] is "--diagnose" or "--preview" or "--widget-preview" or "--widget-detailed-preview")
            {
                var data = CodexClient.FetchAsync().GetAwaiter().GetResult();
                if (args[0] is "--widget-preview" or "--widget-detailed-preview")
                {
                    var detailed = args[0] == "--widget-detailed-preview";
                    using var bitmap = TaskbarWidget.DrawWidget(data, true, detailed ? TaskbarWidget.DetailedSize : TaskbarWidget.LogicalSize, detailed: detailed);
                    using var canvas = new Bitmap(detailed ? 430 : 300, 70);
                    using var g = Graphics.FromImage(canvas);
                    g.Clear(Color.FromArgb(26, 33, 42));
                    g.DrawImageUnscaled(bitmap, 18, 18);
                    using var font = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
                    using var brush = new SolidBrush(Palette.Muted);
                    g.DrawString(detailed ? L10n.T("CLT · Detaylı") : L10n.T("CLT · Basit"), font, brush, detailed ? 250 : 127, 28);
                    canvas.Save(output, System.Drawing.Imaging.ImageFormat.Png);
                }
                else if (args[0] == "--preview")
                {
                    using var popup = new Popup(() => { }, () => { }, () => { });
                    popup.SetState(data, null, false, false, true);
                    popup.Location = new Point(-30000, -30000);
                    popup.Show();
                    Application.DoEvents();
                    using var bitmap = new Bitmap(popup.Width, popup.Height);
                    popup.DrawToBitmap(bitmap, popup.ClientRectangle);
                    bitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png);
                }
                else
                {
                    // Deliberately omit account ID, email, tokens, credits and raw server responses.
                    File.WriteAllText(output, JsonSerializer.Serialize(new { ok = true, notificationAccountKnown = data.AccountKey is not null, recoveryPermissionKnown = data.OrdinaryUsageAllowed is not null, fetchedAt = data.FetchedAt, buckets = data.Buckets.Select(b => new { name = b.Name, primaryRemaining = b.Primary?.Remaining, secondaryRemaining = b.Secondary?.Remaining, primaryReset = b.Primary?.ResetText, secondaryReset = b.Secondary?.ResetText }) }, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            else throw new ArgumentException(L10n.T("Desteklenen seçenekler: --self-test, --diagnose, --preview; ardından çıktı dosyası."));
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(output + ".error.txt", ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    private static List<string> RunTests()
    {
        var passed = new List<string>();
        void Check(string name, bool value) { if (!value) throw new Exception("FAILED: " + name); passed.Add(name); }
        UsageSnapshot Parse(string json) { using var doc = JsonDocument.Parse(json); return UsageSnapshot.Parse(doc.RootElement); }
        var live = Parse("""{"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":83,"windowDurationMins":300,"resetsAt":1792071126},"secondary":{"usedPercent":13,"windowDurationMins":10080,"resetsAt":1792657926},"planType":"plus"}}}""");
        Check("Used percentage is converted to remaining", live.Main.Primary!.Remaining == 17 && live.Main.Secondary!.Remaining == 87);
        Check("Window durations are read from server", live.Main.Primary.Duration == L10n.T("5 saatlik") && live.Main.Secondary!.Duration == L10n.T("Haftalık"));
        var legacy = Parse("""{"rateLimits":{"primary":{"usedPercent":24.1,"windowDurationMins":60},"secondary":null}}""");
        Check("Legacy payload and fractional usage", legacy.Main.Primary!.Remaining == 75 && legacy.Main.Secondary is null);
        Check("Missing reset does not invent a date", legacy.Main.Primary!.ResetText == L10n.T("Bilinmiyor"));
        var nullable = Parse("""{"rateLimits":{"primary":{"usedPercent":12,"windowDurationMins":null,"resetsAt":null}}}""");
        Check("Explicit null metadata is tolerated", nullable.Main.Primary!.Resets is null && nullable.Main.Primary.Remaining == 88);
        var multi = Parse("""{"rateLimitsByLimitId":{"extra":{"limitName":"Other","primary":{"usedPercent":-2,"windowDurationMins":15}},"codex":{"secondary":{"usedPercent":130,"windowDurationMins":10080}}}}""");
        Check("Codex bucket preferred regardless of ordering", multi.Main.Id == "codex");
        Check("Remaining values clamped", multi.Main.Secondary!.Remaining == 0 && multi.Buckets[0].Primary!.Remaining == 100);
        var fallback = Parse("""{"rateLimitsByLimitId":{},"rateLimits":{"primary":{"usedPercent":0,"windowDurationMins":300}}}""");
        Check("Empty map falls back to legacy", fallback.Main.Primary!.Remaining == 100);
        var unavailable = false;
        try { Parse("""{"rateLimits":null,"rateLimitsByLimitId":{}}"""); } catch (InvalidOperationException) { unavailable = true; }
        Check("No quota data is not displayed as zero", unavailable);
        var invalid = false;
        try { Parse("""{"rateLimits":{"primary":{"usedPercent":"unknown"}}}"""); } catch (InvalidOperationException) { invalid = true; }
        Check("Malformed percentages rejected", invalid);
        var now = DateTimeOffset.Now;
        Check("Expired reset waits for server confirmation", new UsageWindow(100, 300, now.AddSeconds(-1)).Countdown(now) == L10n.T("Yenilenme bekleniyor"));
        Check("Countdown rounds partial minutes up", new UsageWindow(0, 300, now.AddSeconds(10)).Countdown(now) == L10n.T("{0} dk sonra yenilenir", 1));
        var screen = new Rectangle(0, 0, 2560, 1440);
        Check("True full-screen content may hide widget", TaskbarWidget.CoversScreen(screen, screen, false, false));
        Check("Maximized apps keep widget visible", !TaskbarWidget.CoversScreen(screen, screen, true, false));
        Check("Task switcher and shell surfaces keep widget visible", !TaskbarWidget.CoversScreen(screen, screen, false, true));
        Check("Invisible resize borders are not full-screen content", !TaskbarWidget.CoversScreen(new Rectangle(-8, -8, 2576, 1456), screen, false, false));
        Check("Ordinary windows keep widget visible", !TaskbarWidget.CoversScreen(new Rectangle(100, 100, 1200, 800), screen, false, false));
        foreach (var n in new int?[] { null, 0, 9, 17, 87, 100 }) { using var icon = TrayDrawing.Create(n); Check($"Tray icon renders {n?.ToString() ?? "unavailable"}", icon.Width == 32 && icon.Height == 32); }
        using (var icon = TrayDrawing.Create(12))
        using (var bitmap = icon.ToBitmap())
        {
            var columns = Enumerable.Range(0, bitmap.Width).Where(x => Enumerable.Range(0, bitmap.Height).Any(y => bitmap.GetPixel(x, y).A > 128)).ToArray();
            Check("Two-digit tray value fits on one line", columns.Length > 0 && columns[^1] - columns[0] > 16);
        }
        using (var bitmap = TaskbarWidget.DrawWidget(live, true, TaskbarWidget.LogicalSize))
        {
            Check("Outside widget content is click-through", bitmap.GetPixel(0, 0).A == 0 && bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).A == 0);
            for (var row = 0; row < 2; row++)
            {
                var painted = 0;
                for (var x = 0; x < bitmap.Width; x++) for (var y = row * bitmap.Height / 2; y < (row + 1) * bitmap.Height / 2; y++) if (bitmap.GetPixel(x, y).A > 128) painted++;
                Check($"Widget row {row + 1} contains visible text", painted > 70);
            }
        }
        var originalUi = System.Globalization.CultureInfo.CurrentUICulture;
        var originalRegion = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // Deliberately mismatch region and display language to catch accidental regional detection.
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            foreach (var language in new[] { "tr-TR", "en-US", "en-GB", "de-DE" })
            {
                System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(language);
                var turkish = language == "tr-TR";
                Check($"{language}: display language and fallback", L10n.IsTurkish == turkish && L10n.T("Şimdi yenile") == (turkish ? "Şimdi yenile" : "Refresh now"));
                Check($"{language}: percentage placement", L10n.Percent(72) == (turkish ? "%72" : "72%"));
                Check($"{language}: durations", new UsageWindow(0, 90, null).Duration == (turkish ? "1,5 saatlik" : "1.5-hour"));
                Check($"{language}: countdown", new UsageWindow(0, 300, now.AddHours(2).AddMinutes(15)).Countdown(now) == (turkish ? "2 sa 15 dk sonra yenilenir" : "Resets in 2h 15m"));
                Check($"{language}: reset month", new UsageWindow(0, 300, new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero)).ResetText.Contains(turkish ? "Eki" : "Oct"));
                using var popup = new Popup(() => { }, () => { }, () => { });
                popup.SetState(live, null, false, false, true);
                Check($"{language}: popup controls", popup.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<Button>()).Any(b => b.Text == (turkish ? "Şimdi yenile" : "Refresh now")));
            }
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = originalUi;
            System.Globalization.CultureInfo.CurrentCulture = originalRegion;
        }
        FeatureTests.Run(Check);
        return passed;
    }
}
