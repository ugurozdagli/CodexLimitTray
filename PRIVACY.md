# Privacy

CLT uses the Codex installation and signed-in session of the person running it. No maintainer credentials or shared account are part of the application.

## What CLT handles

- Rate-limit percentages, window durations, reset timestamps, and optional limit/plan labels returned by Codex.
- Display preferences: widget visibility, simple/detailed mode, tray selection, horizontal offset, and position lock.
- An optional current-user startup entry containing the executable path.

The latest usage snapshot stays in memory during normal operation. Preferences are stored at `%LOCALAPPDATA%\CodexLimitTray\settings.json`. Startup uses `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `CodexLimitTray`.

## Authentication and networking

CLT does not directly open Codex's `auth.json`, request a password/API key, or store authentication tokens. The local Codex app-server manages its own authentication. Responses may include additional account metadata; CLT's display parser selects the usage fields it needs and does not persist raw responses.

CLT has no telemetry endpoint, analytics database, or incoming network listener. The Codex helper contacts Codex services as necessary and remains subject to its own configuration and OpenAI's policies. "No CLT telemetry" does not mean "no network activity by Codex."

## Explicit diagnostics

`--diagnose` writes usage percentages and reset information to a user-selected file; live `--preview` modes create images from live data. These are explicit diagnostics, not background logging. Errors can contain local file paths. Do not publish these files without reviewing them.

`--demo-previews` uses fictional data and does not start Codex. The images shipped in the repository use that mode. Account credentials and local settings must never be included in release ZIPs.

## Removing local data

Turn off **Windows ile başlat**, exit CLT, remove the application folder, and optionally delete `%LOCALAPPDATA%\CodexLimitTray`. CLT does not sign you out of Codex or delete Codex's files.
