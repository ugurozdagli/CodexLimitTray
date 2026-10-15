using System.Text.Json;

namespace CodexLimitTray;

internal sealed record UsageWindow(double Used, int Minutes, DateTimeOffset? Resets)
{
    public int Remaining => (int)Math.Floor(Math.Clamp(100 - Used, 0, 100));
    public string Duration => Minutes <= 0 ? L10n.T("Limit") : Minutes == 10080 ? L10n.T("Haftalık") : Minutes == 300 ? L10n.T("5 saatlik") : Minutes >= 1440 ? L10n.T("{0} günlük", (Minutes / 1440.0).ToString("0.#", L10n.Culture)) : Minutes >= 60 ? L10n.T("{0} saatlik", (Minutes / 60.0).ToString("0.#", L10n.Culture)) : L10n.T("{0} dakikalık", Minutes);
    public string CompactDuration => Minutes <= 0 ? "—" : Minutes == 10080 ? L10n.T("Hf") : Minutes >= 1440 ? L10n.T("{0}g", (Minutes / 1440.0).ToString("0.#", L10n.Culture)) : Minutes >= 60 ? L10n.T("{0}s", (Minutes / 60.0).ToString("0.#", L10n.Culture)) : L10n.T("{0}dk", Minutes);
    public string CompactCountdown(DateTimeOffset now)
    {
        if (Resets is not { } reset) return "—";
        var left = reset - now;
        if (left <= TimeSpan.Zero) return "…";
        return left.TotalDays >= 1 ? L10n.T("{0}g {1}s", (int)left.TotalDays, left.Hours) : left.TotalHours >= 1 ? L10n.T("{0}s {1}dk", (int)left.TotalHours, left.Minutes) : L10n.T("{0}dk", Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)));
    }
    public string ResetText => Resets is { } reset ? reset.ToLocalTime().ToString("dd MMM HH:mm", L10n.Culture) : L10n.T("Bilinmiyor");
    public string Countdown(DateTimeOffset now)
    {
        if (Resets is not { } reset) return L10n.T("Yenilenme saati bilinmiyor");
        var left = reset - now;
        if (left <= TimeSpan.Zero) return L10n.T("Yenilenme bekleniyor");
        return left.TotalDays >= 1 ? L10n.T("{0} gün {1} sa sonra yenilenir", (int)left.TotalDays, left.Hours) : left.TotalHours >= 1 ? L10n.T("{0} sa {1} dk sonra yenilenir", (int)left.TotalHours, left.Minutes) : L10n.T("{0} dk sonra yenilenir", Math.Max(1, (int)Math.Ceiling(left.TotalMinutes)));
    }
}

internal sealed record UsageBucket(string Id, string Name, UsageWindow? Primary, UsageWindow? Secondary, string? Plan);
internal sealed record UsageSnapshot(List<UsageBucket> Buckets, DateTimeOffset FetchedAt)
{
    // One-way identity, memory only; never included in settings, diagnostics, or releases.
    [System.Text.Json.Serialization.JsonIgnore] public string? AccountKey { get; init; }
    public bool? OrdinaryUsageAllowed { get; init; }
    public UsageBucket Main => Buckets.FirstOrDefault(b => b.Id == "codex") ?? Buckets[0];
    public static UsageSnapshot Parse(JsonElement root)
    {
        var buckets = new List<UsageBucket>();
        if (root.TryGetProperty("rateLimitsByLimitId", out var map) && map.ValueKind == JsonValueKind.Object)
            foreach (var prop in map.EnumerateObject()) Add(prop.Value, prop.Name);
        if (buckets.Count == 0 && root.TryGetProperty("rateLimits", out var legacy) && legacy.ValueKind == JsonValueKind.Object) Add(legacy, "codex");
        if (buckets.Count == 0) throw new InvalidOperationException(L10n.T("Bu hesap için kullanım limiti verisi dönmedi. Codex'e ChatGPT hesabınla giriş yaptığından emin ol."));
        var accountId = Text(root, "accountId");
        return new(buckets, DateTimeOffset.Now)
        {
            AccountKey = string.IsNullOrWhiteSpace(accountId) ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(accountId))),
            OrdinaryUsageAllowed = root.TryGetProperty("ordinaryUsageAllowed", out var allowed) && allowed.ValueKind is JsonValueKind.True or JsonValueKind.False ? allowed.GetBoolean() : null
        };

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
