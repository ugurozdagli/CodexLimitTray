# CodexLimitTray · CLT

Windows görev çubuğunda Codex aboneliğinin **kalan** kullanım yüzdeleri. NetSpeedTray tarzında iki satır (`5s %12` / `Hf %86`), Türkçe arayüz, 60 saniyede bir yenileme ve ayrıntılı panel.

## Çalıştırma

İndirilen paketi kalıcı bir klasöre çıkarıp `CodexLimitTray.exe` dosyasına çift tıkla. Kaynaktan derlediysen `release\CodexLimitTray.exe` dosyasını çalıştır. İki satırlı gösterge görev çubuğunun sağında, sistem tepsisi alanının hemen solunda görünür. NetSpeedTray çalışıyorsa onun soluna yerleşmeye çalışır. `^` menüsündeki ek CLT simgesi yönetim için de kullanılabilir.

- **5s:** 5 saatlik kalan hak. **Hf:** Haftalık kalan hak.
- **Sağ tık → Görünüm → Basit / Detaylı:** Basit, kompakt iki satırda yüzdeyi gösterir. Detaylı, limit adını ve yenilenme tarih/saatini de aynı satırlara ekler. Seçim kaydedilir; değişiklik anında uygulanır.
- **Konum:** Göstergeyi yatay sürükle; konum kaydedilir. Sağ tık → Gösterge konumunu kilitle / Gösterge konumunu sıfırla.
- **Görev çubuğunda iki satır:** Sağ tık menüsünden açılıp kapatılır.
- **Görünürlük:** Görev çubuğunu takip eder; tam ekran uygulamada gizlenir. Ana monitördeki yatay Windows görev çubuğu desteklenir.

- **Simge üzerindeki sayı:** Kalan yüzde; yüzde işareti küçük simgede yer kazanmak için gösterilmez. Varsayılan kısa dönem (bu hesapta 5 saatlik).
- **Sol tık:** İki limiti, kalan hakkı, geri sayımı, yerel yenilenme tarihini ve son kontrolü gösterir. Esc / dışarı tıklama paneli gizler.
- **Üzerine gel:** Kısa kullanım özeti.
- **Sağ tık:** Yenile, kısa dönem / haftalık yüzdeyi seç, Windows ile başlat, görev çubuğu ayarları veya çıkış.
- **Renkler:** %35 üstü yeşil; %16–35 sarı; %0–15 kırmızı.
- **Gri çizgi:** Veri bekleniyor, bağlantı hatası veya veri 3 dakikadan eski. Önceki veri panelde "eski veri" olarak kalır. Bir limit sunulmuyorsa o limit için sayı uydurulmaz.
- **Windows ile başlat:** Varsayılan kapalıdır; panelden veya sağ tık menüsünden açılır. Kullanıcının Run kayıt defteri anahtarına mevcut EXE yolunu ekler. EXE'yi taşırsan kapatıp tekrar aç.

İki satırlı gösterge, Explorer'a kod eklemeden görev çubuğunun üstünde saydam bir katman olarak çizilir. Windows bu katman için görev düğmelerinden yer ayırmaz; gerekirse sürükleyerek boş bir alana taşı. İkinci kez çalıştırmak var olan CLT panelini açar. Küçük tray simgesinin ayrıca kısa dönem/haftalık yüzde seçimi vardır.

## Veri kaynağı ve gizlilik

CLT, bilgisayardaki `codex.exe app-server` ile standart girdi/çıktı üzerinden konuşur: `initialize`, `initialized`, `account/rateLimits/read`. ChatGPT ile giriş yapılmış Codex oturumu gerekir. API faturalandırma bakiyesi ile abonelik limitleri aynı şey değildir.

CLT kimlik doğrulama dosyalarını doğrudan okumaz, parola/API anahtarı istemez ve saklamaz. Oturumu Codex'in kendi uygulama sunucusu yönetir. Sohbet, model isteği veya araç çalıştırmaz; yalnızca limit okur. Yenileme başına yardımcı süreç başlatılır ve sonunda kapatılır. CLT telemetri veya kullanım geçmişi kaydetmez; sunucu stderr çıktısını saklamaz. Codex'in kendi ayarları ona uygulanır.

Tercih dosyası: `%LOCALAPPDATA%\CodexLimitTray\settings.json` (tray modu, iki satırlı gösterge görünürlüğü, konum ve kilitleme tercihi).

Codex masaüstü uygulamasının `%LOCALAPPDATA%\OpenAI\Codex\bin\*\codex.exe` dosyası, sonra PATH aranır. Özel bir CLI için `CLT_CODEX_PATH` ortam değişkenine tam EXE yolu verilebilir.

Kaynak: https://learn.chatgpt.com/docs/app-server#6-rate-limits-chatgpt

## Derleme

.NET 10 SDK ve Windows gerekir. Harici NuGet paketi yoktur.

```powershell
.\build.ps1
# .NET Desktop Runtime gerektirmeyen daha büyük bir EXE için:
.\build.ps1 -SelfContained
```

Varsayılan derleme `.NET 10 Windows Desktop Runtime` gerektirir. GitHub sürümündeki self-contained ZIP paketi runtime içerir. İlk sürüm kod imzalı değildir.

## Doğrulama

```powershell
$p = Start-Process .\release\CodexLimitTray.exe -ArgumentList '--self-test', 'test-results.json' -PassThru -Wait
$p.ExitCode
# Gerçek Codex bağlantısı; yalnızca yüzdeler ve yenilenme zamanları:
Start-Process .\release\CodexLimitTray.exe -ArgumentList '--diagnose', 'connection-results.json' -Wait
# Canlı veriyle panelin PNG önizlemesi:
Start-Process .\release\CodexLimitTray.exe -ArgumentList '--preview', 'preview.png' -Wait
# Tray mesaj döngüsünde gerçek bağlantı ve hata durumu:
Start-Process .\release\CodexLimitTray.exe -ArgumentList '--smoke-test', 'tray-test.json' -Wait
Start-Process .\release\CodexLimitTray.exe -ArgumentList '--smoke-test-error', 'error-state-test.json' -Wait
```

Testler limit pencereleri, kalan yüzde hesabı, çoklu/legacy veri biçimi, eksik/geçersiz veriler, geri sayım ve simge çizimini kontrol eder. Başarılı tanılama çıkış kodu 0; hata 1. Hata özeti belirtilen çıktı yoluna `.error.txt` eklenerek yazılır; ham sunucu yanıtı kaydedilmez.

27 otomatik kontrol, tray mesaj döngüsünde canlı bağlantı ve katman sırası testi, ayrıca eksik Codex dosyasıyla hata durumu testi geçmiştir. Tam ekran / büyütülmüş pencere ayrımı ve görev değiştirici için regresyon kontrolleri vardır. Panel ve iki satırlı gösterge PNG önizlemeleri görsel olarak kontrol edilmiştir.

Gösterge, Windows görev çubuğuna pencere sahipliğiyle bağlanır ve pencere geçişi/sıra değişimi olaylarını dinler. Görev çubuğuna tıklanırken bir saniyelik yeniden öne alma döngüsünü beklemez. Normal büyütülmüş pencereler ve görev değiştirici tam ekran uygulama sayılmaz. Detay paneli, açıkça etkinleştirilmiş gri tonlu yazı yumuşatması ve Windows'un tek doğal pencere çerçevesini kullanır; tüm pencereyi saydamlaştırarak metni bulanıklaştırmaz.

## Kaldırma

Windows ile başlat seçeneğini kapat, sağ tık → Çıkış, ardından proje klasörünü kaldır. İstersen `%LOCALAPPDATA%\CodexLimitTray` tercih klasörünü de silebilirsin. Codex oturumu değişmez.
