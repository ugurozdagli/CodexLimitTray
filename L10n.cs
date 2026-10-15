using System.Globalization;

namespace CodexLimitTray;

// Follow the Windows display language, independently of regional number/date settings.
internal static class L10n
{
    public static bool IsTurkish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(IsTurkish ? "tr-TR" : "en-US");
    public static string Percent(int remaining) => IsTurkish ? $"%{remaining}" : $"{remaining}%";
    public static string T(string turkish, params object?[] args)
    {
        var text = IsTurkish ? turkish : English.GetValueOrDefault(turkish, turkish);
        return args.Length == 0 ? text : string.Format(Culture, text, args);
    }
    private static readonly Dictionary<string, string> English = new()
    {
        ["Windows ile başlat"] = "Start with Windows",
        ["Şimdi yenile"] = "Refresh now",
        ["Tray: 5 saatlik"] = "Tray: 5-hour",
        ["Yenileniyor…"] = "Refreshing…",
        ["Tray: haftalık"] = "Tray: weekly",
        ["Tray: kısa dönem"] = "Tray: short-term",
        ["Codex oturumuna bağlanıyor"] = "Connecting to Codex",
        ["Kalan kullanım hakkın"] = "Your remaining usage",
        ["Güncel"] = "Up to date",
        ["Eski veri"] = "Outdated",
        ["Bekleniyor"] = "Waiting",
        ["kalan"] = "left",
        ["eski veri"] = "outdated",
        ["Codex'ten kullanım verisi alınıyor…"] = "Fetching usage from Codex…",
        ["CLT\n\nKullanım bilgilerin burada görünecek.\nCodex oturumun açık olmalı."] = "CLT\n\nYour usage will appear here.\nSign in to Codex to get started.",
        ["Kısa dönem kalan %"] = "Short-term remaining %",
        ["Haftalık kalan %"] = "Weekly remaining %",
        ["Görev çubuğunda iki satır"] = "Two-line taskbar widget",
        ["Gösterge konumunu kilitle"] = "Lock widget position",
        ["Görünüm"] = "Appearance",
        ["Basit"] = "Simple",
        ["Detaylı"] = "Detailed",
        ["Gösterge konumunu sıfırla"] = "Reset widget position",
        ["Tray görünürlük ayarları"] = "Tray visibility settings",
        ["Çıkış"] = "Exit",
        ["Görünüm tercihi kaydedilemedi."] = "Could not save appearance preferences.",
        ["Windows başlangıç ayarı kaydedilemedi."] = "Could not save the Windows startup setting.",
        ["Görev çubuğuna sağ tıkla → Görev çubuğu ayarları → Diğer sistem tepsisi simgeleri."] = "Right-click the taskbar → Taskbar settings → Other system tray icons.",
        ["Kullanım verisi alınamadı. Codex oturumunu kontrol edip tekrar dene."] = "Could not fetch usage. Check your Codex sign-in and try again.",
        ["CLT · Codex kalan kullanım"] = "CLT · Codex remaining usage",
        ["Veri bekleniyor…"] = "Waiting for usage…",
        ["Güncel veri alınamadı; tıkla"] = "Could not refresh; click for details",
        ["CLT · Görev çubuğu göstergesi"] = "CLT · Taskbar widget",
        ["CLT · Güncel kullanım verisi bekleniyor"] = "CLT · Waiting for current usage",
        ["Kısa"] = "Short",
        ["Haftalık"] = "Weekly",
        ["5 saatlik"] = "5-hour",
        ["5s"] = "5h",
        ["Ks"] = "ST",
        ["Hf"] = "Wk",
        ["Bilinmiyor"] = "Unknown",
        ["Yenilenme saati bilinmiyor"] = "Reset time unknown",
        ["Yenilenme bekleniyor"] = "Waiting for reset",
        ["Bu hesap için kullanım limiti verisi dönmedi. Codex'e ChatGPT hesabınla giriş yaptığından emin ol."] = "No usage limits were returned for this account. Sign in to Codex with your ChatGPT account.",
        ["CLT_CODEX_PATH geçerli bir EXE dosyasının tam yolu olmalı."] = "CLT_CODEX_PATH must be the absolute path to a valid EXE file.",
        ["codex.exe bulunamadı. Codex masaüstü uygulamasını veya CLI'ı kur; gerekirse CLT_CODEX_PATH değişkenini ayarla."] = "codex.exe was not found. Install the Codex desktop app or CLI; set CLT_CODEX_PATH if needed.",
        ["Codex başlatılamadı."] = "Could not start Codex.",
        ["Codex 35 saniyede yanıt vermedi. Bağlantını ve Codex oturumunu kontrol et."] = "Codex did not respond within 35 seconds. Check your connection and Codex sign-in.",
        ["Codex kullanım verisini okuyamadı. Codex'te ChatGPT oturumunu ve internet bağlantını kontrol et."] = "Codex could not read usage. Check your ChatGPT sign-in in Codex and your internet connection.",
        ["Codex geçersiz bir yanıt döndürdü."] = "Codex returned an invalid response.",
        ["Codex bağlantısı kapandı. Codex oturumunu kontrol et."] = "The Codex connection closed. Check your Codex sign-in.",
        ["Windows görev çubuğu bulunamadı."] = "Windows taskbar was not found.",
        ["CLT · Detaylı"] = "CLT · Detailed",
        ["CLT · Basit"] = "CLT · Simple",
        ["Desteklenen seçenekler: --self-test, --diagnose, --preview; ardından çıktı dosyası."] = "Supported options: --self-test, --diagnose, --preview; followed by an output file.",
        ["{0} günlük"] = "{0}-day",
        ["{0} saatlik"] = "{0}-hour",
        ["{0} dakikalık"] = "{0}-minute",
        ["{0} gün {1} sa sonra yenilenir"] = "Resets in {0}d {1}h",
        ["{0} sa {1} dk sonra yenilenir"] = "Resets in {0}h {1}m",
        ["{0} dk sonra yenilenir"] = "Resets in {0}m",
        ["{0} · Son kontrol {1:HH:mm:ss}\n60 saniyede bir otomatik yenilenir."] = "{0} · Last checked {1:HH:mm:ss}\nRefreshes automatically every 60 seconds.",
        ["{0} {1} yüzde {2} kaldı. Yenilenme {3}"] = "{0} {1}: {2}% remaining. Resets {3}",
        ["Yenilenme  ·  {0}"] = "Resets  ·  {0}",
        ["CLT · Kalan kullanım\n{0}: {1}\n{2}: {3}\nSol tık: ayrıntılar · Sağ tık: ayarlar"] = "CLT · Remaining usage\n{0}: {1}\n{2}: {3}\nLeft click: details · Right click: settings",
    };
}
