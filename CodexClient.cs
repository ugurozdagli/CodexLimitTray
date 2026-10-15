using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CodexLimitTray;

// Short-lived stdio connection: no network listener, no prompts or model turns.
internal static class CodexClient
{
    public static string FindExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("CLT_CODEX_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.IsPathFullyQualified(configured) && Path.GetExtension(configured).Equals(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(configured)
                ? configured : throw new FileNotFoundException("CLT_CODEX_PATH geçerli bir EXE dosyasının tam yolu olmalı.");
        // The desktop app's versioned binary is preferred over potentially old PATH entries.
        var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(desktop))
        {
            var candidates = Directory.EnumerateDirectories(desktop).Select(d => Path.Combine(d, "codex.exe")).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc);
            var candidate = candidates.FirstOrDefault();
            if (candidate is not null) return candidate;
        }
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var searchDirectory = directory.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(searchDirectory) || !Path.IsPathFullyQualified(searchDirectory)) continue;
            var candidate = Path.Combine(searchDirectory, "codex.exe");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("codex.exe bulunamadı. Codex masaüstü uygulamasını veya CLI'ı kur; gerekirse CLT_CODEX_PATH değişkenini ayarla.");
    }

    public static async Task<UsageSnapshot> FetchAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        var ct = timeout.Token;
        var start = new ProcessStartInfo(FindExecutable())
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add("app-server");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Codex başlatılamadı.");
        using var cancelProcess = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });
        // Drain stderr without logging it; server diagnostics may contain private metadata.
        var stderr = Task.Run(async () => { try { while (await process.StandardError.ReadLineAsync(ct) is not null) { } } catch (OperationCanceledException) { } catch (IOException) { } });
        try
        {
            await Send(new { id = 1, method = "initialize", @params = new { clientInfo = new { name = "codex_limit_tray", title = "CodexLimitTray", version = "1.2.0" } } });
            await Receive(1);
            await Send(new { method = "initialized", @params = new { } });
            await Send(new { id = 2, method = "account/rateLimits/read", @params = new { } });
            return UsageSnapshot.Parse(await Receive(2));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Codex 35 saniyede yanıt vermedi. Bağlantını ve Codex oturumunu kontrol et.");
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            // Ensure no helper process survives a refresh or application exit.
            try { await process.WaitForExitAsync(CancellationToken.None); await stderr; } catch (IOException) { }
        }

        async Task Send(object message) => await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), ct);
        async Task<JsonElement> Receive(int id)
        {
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                JsonDocument doc;
                try { doc = JsonDocument.Parse(line); } catch (JsonException) { continue; }
                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out var responseId) && responseId.ValueKind == JsonValueKind.Number && responseId.TryGetInt32(out var value) && value == id)
                    {
                        if (root.TryGetProperty("error", out _)) throw new InvalidOperationException("Codex kullanım verisini okuyamadı. Codex'te ChatGPT oturumunu ve internet bağlantını kontrol et.");
                        if (root.TryGetProperty("result", out var result)) return result.Clone();
                        throw new InvalidOperationException("Codex geçersiz bir yanıt döndürdü.");
                    }
                    // Explicitly reject unexpected requests; never grant approvals or execute tools.
                    if (root.TryGetProperty("id", out var requestId) && root.TryGetProperty("method", out _))
                        await Send(new { id = requestId.Clone(), error = new { code = -32601, message = "Read-only usage client" } });
                }
            }
            throw new IOException("Codex bağlantısı kapandı. Codex oturumunu kontrol et.");
        }
    }
}
