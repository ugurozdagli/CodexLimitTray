# Privacy

CLT uses the Codex installation and signed-in session of the person running it. No maintainer credentials or shared account are part of the application.

## What CLT handles

- Rate-limit percentages, window durations, reset timestamps, and optional limit/plan labels returned by Codex.
- Display preferences: visibility, view, tray selection, position, theme, text scale, reset format, monitor identity, and notification thresholds.
- When supplied by Codex, a SHA256 account fingerprint and ordinary-usage permission for safe notification comparison. The raw account identifier is not retained in the usage model; the fingerprint stays in memory and is excluded from diagnostic serialization.
- An optional current-user startup entry containing the executable path.

The latest usage snapshot stays in memory during normal operation. Preferences are stored at `%LOCALAPPDATA%\CodexLimitTray\settings.json`, with the previous copy at `settings.json.bak`. Startup uses `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `CodexLimitTray`.

## Authentication and networking

CLT does not directly open Codex's `auth.json`, request a password/API key, or store authentication tokens. The local Codex app-server manages its own authentication. Responses may include additional account metadata; CLT's display parser selects the usage fields it needs and does not persist raw responses.

CLT has no telemetry endpoint, analytics database, or incoming network listener. The Codex helper contacts Codex services as necessary and remains subject to its own configuration and OpenAI's policies. "No CLT telemetry" does not mean "no network activity by Codex."

## Optional update checks and notifications

Only when you choose **Check for updates**, CLT requests the public GitHub Releases API using its application version as the User-Agent. GitHub receives ordinary connection metadata such as your IP address. No Codex account identifiers, usage percentages, or authentication credentials are sent. Opening a release page uses your default browser. CLT does not automatically download or execute an update.

Notifications are evaluated locally from existing refreshes. There is no additional notification service or polling. Account fingerprints and notification baselines are not saved across app restarts. Preferences include whether you enabled alerts and the thresholds you selected.

## Explicit diagnostics

`--diagnose` writes usage percentages and reset information to a user-selected file; live `--preview` modes create images from live data. These are explicit diagnostics, not background logging. Errors can contain local file paths. Do not publish these files without reviewing them.

`--demo-previews` uses fictional data and does not start Codex. The images shipped in the repository use that mode. Account credentials and local settings must never be included in release ZIPs.

## Removing local data

Turn off **Windows ile başlat**, exit CLT, remove the application folder, and optionally delete `%LOCALAPPDATA%\CodexLimitTray`. CLT does not sign you out of Codex or delete Codex's files.
