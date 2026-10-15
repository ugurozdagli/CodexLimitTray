using System.Text.Json;

namespace CodexLimitTray;

internal enum ThemeMode { System, Dark, Light }
internal enum ResetDisplay { Date, Countdown, Hidden }

internal sealed class Settings
{
    public bool Weekly { get; set; }
    public bool WidgetEnabled { get; set; } = true;
    public bool WidgetDetailed { get; set; }
    public bool WidgetLocked { get; set; }
    public int WidgetGap { get; set; } = 8;
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public float TextScale { get; set; } = 1;
    public ResetDisplay ResetDisplay { get; set; }
    public string? DisplayKey { get; set; }
    public bool LowQuotaAlerts { get; set; }
    public bool ReplenishmentAlerts { get; set; }
    public int WarningThreshold { get; set; } = 20;
    public int CriticalThreshold { get; set; } = 5;
    internal static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexLimitTray", "settings.json");
    public Settings Normalize()
    {
        if (!Enum.IsDefined(Theme)) Theme = ThemeMode.Dark;
        if (!Enum.IsDefined(ResetDisplay)) ResetDisplay = ResetDisplay.Date;
        TextScale = !float.IsFinite(TextScale) ? 1 : Math.Clamp(TextScale, .85f, 1.2f);
        WarningThreshold = Math.Clamp(WarningThreshold, 2, 99);
        CriticalThreshold = Math.Clamp(CriticalThreshold, 1, WarningThreshold - 1);
        WidgetGap = Math.Clamp(WidgetGap, -20000, 20000);
        return this;
    }
    public static Settings Load() => LoadFrom(FilePath);
    internal static Settings LoadFrom(string path)
    {
        foreach (var candidate in new[] { path, path + ".bak" })
            try { if (JsonSerializer.Deserialize<Settings>(File.ReadAllText(candidate)) is { } settings) return settings.Normalize(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return new();
    }
    public void Save() => SaveTo(FilePath);
    internal void SaveTo(string path)
    {
        Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(this));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
