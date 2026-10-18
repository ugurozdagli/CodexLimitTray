# CodexLimitTray v1.5.0

**For Codex addicts. Just one more prompt.**

A quieter interface for a small Windows utility. Your quota, explained. Your next prompt, hopefully.

## What's changed

- Flat dark/light panels replace gradient cards and decorative badges.
- Smaller, neutral percentages, slimmer usage bars, and a more compact usage panel.
- Settings grouped into Appearance, Notifications, and General.
- A shorter right-click menu with Taskbar and Tray icon submenus.
- Plain checkmarks, neutral highlights, Windows menu fonts, and a system-rendered high-contrast fallback.
- Updated documentation previews in English and Turkish.

Existing preferences, quota polling, optional alerts, and account handling are preserved. The interface still follows your Windows display language: Turkish for Turkish, English otherwise.

## Download and update

Exit CLT, replace your current executable with `CodexLimitTray-v1.5.0-win-x64.exe`, and start it. Alternatively, extract `CodexLimitTray-v1.5.0-win-x64.zip` and run `CodexLimitTray.exe`. Keep your existing executable's name and location if using Start with Windows; otherwise turn that option off and back on from the new copy.

Windows 11 x64 and your own signed-in Codex installation are required. The .NET runtime is bundled. Preferences and their backup remain in your local app-data folder.

## Validation and privacy

116 offline checks pass, plus a message-loop smoke check for an unavailable Codex executable. English and Turkish menu/panel/settings previews were inspected in dark and light themes. Mixed-DPI, Remote Desktop, and other device combinations still need broader testing.

No account credentials or live usage are bundled. Documentation screenshots use fictional demo data. The executable is unsigned; SHA-256 checksums and GitHub build-provenance attestations accompany the release. This project is independent of OpenAI.
