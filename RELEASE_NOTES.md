# CodexLimitTray v1.3.0

**For Codex addicts. Just one more prompt.**

CLT now automatically follows your Windows display language: Turkish on Turkish systems, English on all other display languages. No language setup is needed.

## What's new

- English usage panel, context menu, taskbar labels, tooltips, errors, and accessibility labels.
- Localized percentages (`72%` in English, `%72` in Turkish), reset month names, and countdowns.
- Turkish remains fully supported. Regional settings do not override the display language.
- Updated build tools and automatic SHA256 verification after package upload/download.

Restart CLT after changing your Windows display language. Existing preferences are preserved.

## Download and update

Exit the running CLT instance, replace your previous EXE with `CodexLimitTray-v1.3.0-win-x64.exe`, and run the new file. Keep the same file location/name if using Start with Windows, or turn that option off and on from the new copy. Alternatively, extract `CodexLimitTray-v1.3.0-win-x64.zip` and run `CodexLimitTray.exe`.

The .NET runtime is included. Requires Windows 11 x64 and your own installed Codex application/CLI signed in with ChatGPT. No maintainer account, API key, or session files are included.

Verify the EXE or ZIP against `SHA256SUMS.txt` and the GitHub build-provenance attestation. The executable is unsigned; attestations do not replace Windows code signing or an independent security review. This independent project is not affiliated with OpenAI.
