using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace CodexLimitTray;

internal static class AppInfo
{
    public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.4.0";
    public static string ReleasesUrl => "https://github.com/ugurozdagli/CodexLimitTray/releases";
}

internal sealed record QuotaNotice(string Window, int Remaining, bool Replenished, bool Critical);

// No persistence: a launch, error, settings change, or account change starts a silent baseline.
internal sealed class QuotaAlerts
{
    private UsageSnapshot? previous;
    private readonly HashSet<string> delivered = new();
    public void Reset() { previous = null; delivered.Clear(); }
    public List<QuotaNotice> Observe(UsageSnapshot current, Settings settings)
    {
        var old = previous;
        previous = current;
        var notices = new List<QuotaNotice>();
        if (old is null || string.IsNullOrEmpty(current.AccountKey) || old.AccountKey != current.AccountKey ||
            current.FetchedAt <= old.FetchedAt || current.FetchedAt - old.FetchedAt > TimeSpan.FromMinutes(3)) { delivered.Clear(); return notices; }
        if (delivered.Count > 128) { delivered.Clear(); return notices; }
        foreach (var bucket in current.Buckets)
        {
            var prior = old.Buckets.FirstOrDefault(b => b.Id == bucket.Id && b.Plan == bucket.Plan);
            if (prior is null) continue;
            var before = new[] { prior.Primary, prior.Secondary };
            var after = new[] { bucket.Primary, bucket.Secondary };
            for (var i = 0; i < after.Length; i++)
            {
                var a = before[i]; var b = after[i];
                if (a is null || b is null || b.Minutes <= 0 || a.Minutes != b.Minutes || a.Resets is null || b.Resets is null || b.Resets <= current.FetchedAt) continue;
                var name = bucket.Id == "codex" ? b.Duration : $"{bucket.Name} · {b.Duration}";
                var key = $"{bucket.Id}:{i}:{b.Resets.Value.ToUnixTimeSeconds()}";
                if (a.Resets != b.Resets)
                {
                    // The protocol explicitly requires backend permission before claiming recovery.
                    if (settings.ReplenishmentAlerts && current.OrdinaryUsageAllowed == true && a.Resets <= current.FetchedAt &&
                        b.Resets > a.Resets && b.Remaining - a.Remaining >= 10 && delivered.Add(key + ":recovery"))
                        notices.Add(new(name, b.Remaining, true, false));
                    continue;
                }
                if (!settings.LowQuotaAlerts) continue;
                var critical = a.Remaining > settings.CriticalThreshold && b.Remaining <= settings.CriticalThreshold;
                var warning = a.Remaining > settings.WarningThreshold && b.Remaining <= settings.WarningThreshold;
                if (critical)
                {
                    delivered.Add(key + ":warning");
                    if (delivered.Add(key + ":critical")) notices.Add(new(name, b.Remaining, false, true));
                }
                else if (warning && delivered.Add(key + ":warning")) notices.Add(new(name, b.Remaining, false, false));
            }
        }
        return notices;
    }
}

internal static class UpdateChecker
{
    public static Version? NewerVersion(string tag, string current)
        => Version.TryParse(tag.TrimStart('v'), out var latest) && Version.TryParse(current, out var installed) && latest > installed ? latest : null;
    public static async Task<Version?> CheckAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CodexLimitTray/" + AppInfo.Version);
        using var response = await client.GetAsync("https://api.github.com/repos/ugurozdagli/CodexLimitTray/releases/latest", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!json.RootElement.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String || !Version.TryParse(tag.GetString()?.TrimStart('v'), out _)) throw new JsonException("Invalid release version.");
        return NewerVersion(tag.GetString()!, AppInfo.Version);
    }
}
