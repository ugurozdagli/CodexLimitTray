using System.Globalization;
using System.Text.Json;

namespace CodexLimitTray;

internal static class FeatureTests
{
    public static void Run(Action<string, bool> check)
    {
        var now = DateTimeOffset.Now;
        UsageSnapshot Sample(int remaining, int second, string? account = "test-account", int seconds = 0, DateTimeOffset? reset = null, bool? allowed = true)
            => new([new("codex", "Codex", new(100 - remaining, 300, reset ?? now.AddHours(1)), new(100 - second, 10080, now.AddDays(5)), "plus")], now.AddSeconds(seconds)) { AccountKey = account, OrdinaryUsageAllowed = allowed };
        var settings = new Settings { LowQuotaAlerts = true, ReplenishmentAlerts = true };
        var alerts = new QuotaAlerts();
        check("First low reading is a silent baseline", alerts.Observe(Sample(2, 3), settings).Count == 0);
        alerts.Reset(); alerts.Observe(Sample(30, 80), settings);
        var warning = alerts.Observe(Sample(19, 80, seconds: 10), settings);
        check("Warning fires on a downward threshold crossing", warning.Count == 1 && !warning[0].Critical && !warning[0].Replenished);
        check("Unchanged readings do not repeat alerts", alerts.Observe(Sample(19, 80, seconds: 20), settings).Count == 0);
        alerts.Observe(Sample(21, 80, seconds: 30), settings);
        check("Rounding near threshold does not repeat alerts", alerts.Observe(Sample(19, 80, seconds: 40), settings).Count == 0);
        var critical = alerts.Observe(Sample(4, 80, seconds: 50), settings);
        check("Critical threshold remains independent of warning", critical.Count == 1 && critical[0].Critical);
        alerts.Reset(); alerts.Observe(Sample(30, 30), settings);
        check("Both windows are tracked independently", alerts.Observe(Sample(4, 19, seconds: 10), settings).Count == 2);
        check("Account change starts a silent baseline", alerts.Observe(Sample(1, 1, account: "other-account", seconds: 20), settings).Count == 0);
        alerts.Reset(); alerts.Observe(Sample(30, 30, account: null), settings);
        check("Unknown account identity never guesses continuity", alerts.Observe(Sample(1, 1, account: null, seconds: 10), settings).Count == 0);
        alerts.Reset(); alerts.Observe(Sample(30, 30), settings);
        check("Long observation gaps rebase silently", alerts.Observe(Sample(1, 1, seconds: 240), settings).Count == 0);
        alerts.Reset(); alerts.Observe(Sample(30, 30), settings); alerts.Reset();
        check("Error/reset invalidates notification baseline", alerts.Observe(Sample(1, 1, seconds: 10), settings).Count == 0);
        alerts.Reset(); alerts.Observe(Sample(30, 30), settings);
        check("Disabled alerts remain silent", alerts.Observe(Sample(1, 1, seconds: 10), new Settings()).Count == 0);
        alerts.Reset(); alerts.Observe(Sample(1, 80, reset: now.AddSeconds(5)), settings);
        var replenished = alerts.Observe(Sample(98, 80, seconds: 10, reset: now.AddHours(5)), settings);
        check("Replenishment requires fresh permission and a new reset window", replenished.Count == 1 && replenished[0].Replenished);
        alerts.Observe(Sample(1, 80, seconds: 20, reset: now.AddSeconds(5)), settings);
        check("Old response replay cannot repeat a replenishment notice", alerts.Observe(Sample(98, 80, seconds: 30, reset: now.AddHours(5)), settings).Count == 0);
        alerts.Reset(); alerts.Observe(Sample(1, 80), settings);
        check("Reset changes before expiry do not claim recovery", alerts.Observe(Sample(98, 80, seconds: 10, reset: now.AddHours(5)), settings).Count == 0);
        foreach (var permission in new bool?[] { false, null })
        {
            alerts.Reset(); alerts.Observe(Sample(1, 80, reset: now.AddSeconds(5)), settings);
            check($"Permission {permission?.ToString() ?? "missing"} cannot claim recovery", alerts.Observe(Sample(98, 80, seconds: 10, reset: now.AddHours(5), allowed: permission), settings).Count == 0);
        }
        alerts.Reset(); alerts.Observe(Sample(1, 80), settings);
        check("Percentage correction within a window is not replenishment", alerts.Observe(Sample(98, 80, seconds: 10), settings).Count == 0);
        alerts.Reset(); alerts.Observe(Sample(30, 80), settings);
        check("Plan changes do not produce low-quota warnings", alerts.Observe(Sample(1, 80, seconds: 10) with { Buckets = [Sample(1, 80).Main with { Plan = "business" }] }, settings).Count == 0);

        var original = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var language in new[] { "tr-TR", "en-US" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                check(language + ": unknown duration has a neutral label", new UsageWindow(0, 0, null).Duration == L10n.T("Limit"));
                check(language + ": negative duration is unknown", new UsageWindow(0, -1, null).CompactDuration == "—");
                check(language + ": secondary daily window is not labeled weekly", new UsageWindow(0, 1440, null).CompactDuration == L10n.T("{0}g", "1"));
                check(language + ": compact countdown does not claim reset", new UsageWindow(0, 300, now.AddSeconds(-1)).CompactCountdown(now) == "…");
                var single = new UsageSnapshot([new("codex", "Codex", null, new(12, 1440, now.AddDays(1)), null)], now);
                check(language + ": missing primary collapses to the actual window", TaskbarWidget.VisibleWindows(single).Length == 1 && TaskbarWidget.VisibleWindows(single)[0]!.Minutes == 1440);
                foreach (var theme in new[] { ThemeMode.Dark, ThemeMode.Light })
                {
                    Palette.Apply(theme);
                    foreach (var scale in new[] { .85f, 1f, 1.2f, 1.8f })
                    {
                        var size = TaskbarWidget.MeasureWidget(single, true, scale, true, ResetDisplay.Countdown);
                        using var bitmap = TaskbarWidget.DrawWidget(single, true, size, scale, true, ResetDisplay.Countdown, theme == ThemeMode.Light);
                        check($"{language}/{theme}/{scale}: measured widget leaves margins transparent", bitmap.GetPixel(0, 0).A == 0 && bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).A == 0);
                    }
                    using var popup = new Popup(() => { }, () => { }, () => { });
                    popup.SetState(single, null, false, false, true);
                    using var options = new SettingsForm(new Settings(), () => { }, () => { });
                    check($"{language}/{theme}: panel and settings follow theme", popup.BackColor == Palette.Background && options.BackColor == Palette.Background);
                }
                var small = TaskbarWidget.MeasureWidget(Sample(99, 85), true, .85f, true);
                var large = TaskbarWidget.MeasureWidget(Sample(99, 85), true, 1.2f, true);
                check(language + ": larger text expands widget bounds", large.Width > small.Width && large.Height > small.Height);
                check(language + ": hiding reset reduces width", TaskbarWidget.MeasureWidget(single, true, 1, true, ResetDisplay.Hidden).Width < TaskbarWidget.MeasureWidget(single, true, 1, true).Width);
            }
        }
        finally { CultureInfo.CurrentUICulture = original; Palette.Apply(ThemeMode.Dark); }

        using (var json = JsonDocument.Parse("""{"accountId":"fictional-account","ordinaryUsageAllowed":true,"rateLimits":{"primary":{"usedPercent":10,"windowDurationMins":300}}}"""))
        {
            var parsed = UsageSnapshot.Parse(json.RootElement);
            check("Account identity is hashed and permission parsed", parsed.AccountKey is { Length: 64 } && parsed.AccountKey != "fictional-account" && parsed.OrdinaryUsageAllowed == true);
            check("Snapshot serialization excludes identity", !JsonSerializer.Serialize(parsed).Contains("AccountKey"));
        }
        check("Newer stable release is detected", UpdateChecker.NewerVersion("v1.5.0", "1.4.0") == new Version(1, 5, 0));
        check("Same/older/pre-release tags do not offer updates", UpdateChecker.NewerVersion("v1.4.0", "1.4.0") is null && UpdateChecker.NewerVersion("v1.3.0", "1.4.0") is null && UpdateChecker.NewerVersion("v2.0.0-beta", "1.4.0") is null);
        check("Malformed update tags cannot become download destinations", UpdateChecker.NewerVersion("https://example.invalid/file.exe", "1.4.0") is null);
        foreach (var caption in new[] { "×", L10n.Text("İptal", "Cancel") })
        {
            using var options = new SettingsForm(new Settings(), () => { }, () => { });
            var buttons = options.Controls.OfType<Button>().Concat(options.Controls.OfType<FlowLayoutPanel>().SelectMany(p => p.Controls.OfType<Button>()));
            var button = buttons.Single(b => b.Text == caption);
            // Exercise Button.OnClick without displaying a window or changing registry/settings.
            typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(button, [EventArgs.Empty]);
            check("Modeless settings closes on " + caption, options.IsDisposed);
        }
        var normalized = new Settings { WarningThreshold = 0, CriticalThreshold = 99, Theme = (ThemeMode)99, TextScale = float.NaN }.Normalize();
        check("Invalid preferences normalize safely", normalized.WarningThreshold == 2 && normalized.CriticalThreshold == 1 && normalized.Theme == ThemeMode.Dark && normalized.TextScale == 1);
        var directory = Path.Combine(Path.GetTempPath(), "CLT-settings-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, "settings.json");
            File.WriteAllText(file, """{"Weekly":true,"WidgetGap":22}""");
            var migrated = Settings.LoadFrom(file);
            check("Old settings preserve preferences and safe defaults", migrated.Weekly && migrated.WidgetGap == 22 && !migrated.LowQuotaAlerts && migrated.TextScale == 1);
            migrated.SaveTo(file); migrated.WidgetGap = 30; migrated.SaveTo(file);
            check("Atomic save round-trips with no temporary files", Settings.LoadFrom(file).WidgetGap == 30 && Directory.GetFiles(directory, "*.tmp").Length == 0);
            File.WriteAllText(file, "incomplete json");
            check("Corrupt settings recover the last backup", Settings.LoadFrom(file).WidgetGap == 22);
        }
        finally { Directory.Delete(directory, recursive: true); }
        check("Display choices use non-empty identities", DisplayCatalog.GetDisplays().All(d => !string.IsNullOrWhiteSpace(d.Key)));
        check("Disconnected display falls back to primary", DisplayCatalog.Resolve("nonexistent-test-monitor") == Screen.PrimaryScreen?.DeviceName);
    }
}
