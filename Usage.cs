using System.Text.Json;

namespace CodexLimitTray;

internal sealed record UsageWindow(double Used, int Minutes, DateTimeOffset? Resets)
{
    public int Remaining => (int)Math.Floor(Math.Clamp(100 - Used, 0, 100));
    public string Duration => Minutes == 10080 ? "Haftalık" : Minutes == 300 ? "5 saatlik" : Minutes >= 1440 ? $"{Minutes / 1440} günlük" : Minutes >= 60 ? $"{Minutes / 60.0:0.#} saatlik" : $"{Minutes} dakikalık";
    public string ResetText => Resets is { } reset ? reset.ToLocalTime().ToString("dd MMM HH:mm", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) : "Bilinmiyor";
    public string Countdown(DateTimeOffset now)
    {
        if (Resets is not { } reset) return "Yenilenme saati bilinmiyor";
        var left = reset - now;
        if (left <= TimeSpan.Zero) return "Yenilenme bekleniyor";
        return left.TotalDays >= 1 ? $"{(int)left.TotalDays} gün {left.Hours} sa sonra yenilenir" : left.TotalHours >= 1 ? $"{(int)left.TotalHours} sa {left.Minutes} dk sonra yenilenir" : $"{Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))} dk sonra yenilenir";
    }
}

internal sealed record UsageBucket(string Id, string Name, UsageWindow? Primary, UsageWindow? Secondary, string? Plan);
internal sealed record UsageSnapshot(List<UsageBucket> Buckets, DateTimeOffset FetchedAt)
{
    public UsageBucket Main => Buckets.FirstOrDefault(b => b.Id == "codex") ?? Buckets[0];
    public static UsageSnapshot Parse(JsonElement root)
    {
        var buckets = new List<UsageBucket>();
        if (root.TryGetProperty("rateLimitsByLimitId", out var map) && map.ValueKind == JsonValueKind.Object)
            foreach (var prop in map.EnumerateObject()) Add(prop.Value, prop.Name);
        if (buckets.Count == 0 && root.TryGetProperty("rateLimits", out var legacy) && legacy.ValueKind == JsonValueKind.Object) Add(legacy, "codex");
        if (buckets.Count == 0) throw new InvalidOperationException("Bu hesap için kullanım limiti verisi dönmedi. Codex'e ChatGPT hesabınla giriş yaptığından emin ol.");
        return new(buckets, DateTimeOffset.Now);

        void Add(JsonElement value, string id)
        {
            var first = Window(value, "primary");
            var second = Window(value, "secondary");
            if (first is null && second is null) return;
            buckets.Add(new(id, Text(value, "limitName") ?? id, first, second, Text(value, "planType")));
        }
    }
    private static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static UsageWindow? Window(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object ||
            !w.TryGetProperty("usedPercent", out var used) || used.ValueKind != JsonValueKind.Number || !used.TryGetDouble(out var number) || !double.IsFinite(number)) return null;
        var minutes = w.TryGetProperty("windowDurationMins", out var m) && m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out var n) ? n : 0;
        DateTimeOffset? reset = null;
        if (w.TryGetProperty("resetsAt", out var r) && r.ValueKind == JsonValueKind.Number && r.TryGetInt64(out var unix))
            try { reset = DateTimeOffset.FromUnixTimeSeconds(unix); } catch (ArgumentOutOfRangeException) { }
        return new(number, minutes, reset);
    }
}
