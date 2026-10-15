# Troubleshooting

## A dash instead of usage

Open Codex and check that it is signed in with your own ChatGPT account. Check connectivity, then use **Şimdi yenile**. API-key billing may not provide subscription windows. Missing limits are not assumed to be zero.

CLT prefers `%LOCALAPPDATA%\OpenAI\Codex\bin\*\codex.exe`, then searches `PATH`. Use `CLT_CODEX_PATH` to specify a trusted executable when needed. Restart CLT after changing the environment.

## The widget is hidden or overlaps another button

It hides for full-screen content and follows the primary horizontal taskbar. Exit full-screen mode. The overlay does not reserve taskbar space; drag it to a clear area or right-click and choose **Gösterge konumunu sıfırla**. Unlock its position first if necessary.

The fallback tray icon may be in Windows' `^` overflow menu. Drag it to the visible tray area if desired.

## Startup stopped working after moving the app

Disable **Windows ile başlat**, then enable it again from the app's new permanent location. No administrator access is required.

## Windows shows an unknown publisher

The initial release is not code-signed. Verify that you downloaded the intended repository's release and compare the SHA-256 checksum. Do not disable security features. You may build from source instead.

## Reporting a bug

Include Windows version, display scaling, app/Codex versions, selected view, and reproduction steps. Prefer demo screenshots. Review diagnostics for private data before sharing them. Follow the security policy for vulnerabilities.
