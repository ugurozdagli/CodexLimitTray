# CodexLimitTray v1.4.0

**For Codex addicts. Just one more prompt.**

Make CLT fit your desktop with a rounded Settings panel, optional notifications, and display controls. English and Turkish still follow your Windows display language.

## What's new

- Open Settings from the new gear button or the tray/widget context menu.
- Dark, light, and system panel themes; widget colors follow the taskbar theme independently.
- Small/normal/large widget text with measured bounds and click-through outer margins.
- Date/time, countdown, or hidden reset details in Detailed view.
- Select one monitor's horizontal taskbar; disconnected/unavailable targets fall back to primary.
- Optional warning/critical alerts (editable 20%/5% defaults) and confirmed replenishment notices.
- Silent notification baselines, account-change handling, and per-window threshold deduplication.
- Fix unknown-duration labels, non-weekly secondary labels, and missing-window placeholders.
- Atomic settings saves with backup recovery; existing preferences are preserved.
- Manual GitHub update checks, refresh failure backoff, and refresh after resume.

Notifications are off by default and depend on current Codex account-continuity metadata. Replenishment additionally needs a confirmed new reset window, at least 10 points of added remaining quota, and explicit backend permission. Windows can suppress notifications. CLT must be running; it does not report replenishment while closed.

## Download and update

Exit CLT, replace your old EXE with `CodexLimitTray-v1.4.0-win-x64.exe`, and run it. Keep the existing file name/location if using Start with Windows, or toggle startup off/on from the new copy. Alternatively, extract `CodexLimitTray-v1.4.0-win-x64.zip` and run `CodexLimitTray.exe`.

Windows 11 x64 and your own signed-in Codex installation are required. The .NET runtime is included. Mixed-DPI multi-monitor and Remote Desktop setups need broader device testing; the primary taskbar and fallback path were checked locally.

## Validation and privacy

116 offline checks cover language, notification transitions, rendering, settings migration/recovery, modeless settings controls, update versions, and display fallback. Live Codex/taskbar ownership and the GitHub update endpoint were also checked. Documentation screenshots use fictional usage data.

No account or token is bundled. A one-way account fingerprint is kept only in memory for safe notification comparisons. User-initiated update checks contact GitHub without sending quota/account data and do not download or execute updates.

The executable remains unsigned. SHA256SUMS and GitHub build-provenance attestations are included. This independent project is not affiliated with OpenAI.
