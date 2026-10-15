# Changelog

## 1.4.1 — Settings close controls

- Fix the close and Cancel buttons on the modeless Settings window.
- Add regression checks for both close paths; 116 offline checks now pass.

## 1.4.0 — Personalize your taskbar

- Add a rounded bilingual Settings panel and a gear shortcut from the usage panel.
- Add dark/light/system panel themes, taskbar-aware contrast, and three widget text sizes.
- Add date/countdown/hidden reset details and measured widget bounds.
- Add saved display selection with primary-taskbar fallback.
- Add opt-in warning/critical quota alerts and backend-confirmed replenishment notices.
- Keep notification identity hashes in memory; silence baselines and avoid repeated threshold alerts.
- Fix unknown/non-weekly duration labels and collapse missing widget windows.
- Preserve old preferences with atomic settings replacement and backup recovery.
- Add manual update checks, failure backoff, resume refresh, and a short manual refresh cooldown.
- Expand offline coverage to 114 checks, with live Codex/ownership and update-endpoint smoke checks.

## 1.3.0 — Automatic English and Turkish interface

- Follow the Windows display language: Turkish for Turkish, English for other languages.
- Translate the usage panel, context menu, tooltips, errors, and accessibility labels.
- Localize percentage placement, taskbar abbreviations, countdowns, and reset month names.
- Add display-language tests independent of regional settings, including English fallback.
- Update GitHub Actions dependencies and verify uploaded/downloaded package checksums.

## 1.2.0 — Initial public release

- Simple and Detailed two-line taskbar views with remaining usage and reset dates.
- Rounded dark detail panel with smooth text, countdowns, and remaining bars.
- Sixty-second polling and manual refresh through the local Codex app-server.
- Drag, lock, reset position, optional startup, and a tray fallback.
- Turkish interface with English project documentation.
- Taskbar ownership and foreground event handling to keep the widget stable during window changes.
- Click-through transparent margins and clearer reset separators.
- .NET 10 LTS standalone Windows x64 packaging.
- Offline demo screenshots, automated checks, security/privacy documentation, and MIT licensing.

Earlier builds were local development versions and were not public releases.
