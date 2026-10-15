# Security policy

## Supported versions

Security fixes target the latest published release. Older releases are not maintained separately.

## Report privately

If GitHub's **Security → Report a vulnerability** option is enabled on this repository, use it. Include the affected version, Windows/Codex versions, reproduction steps, impact, and a minimal proof of concept using dummy data.

Do **not** publish credentials, raw Codex authentication files, real account identifiers, or an exploit that exposes another person's data in a public issue. If private reporting is unavailable, open a minimal issue requesting a private reporting channel without exploit details or personal information. No response-time guarantee is offered.

## Trust boundaries

- CLT is a normal Windows desktop process, not a sandbox. It runs with the permissions of the user launching it.
- It launches a local `codex.exe` using argument lists and stdio. A replaced or malicious Codex executable, an untrusted `CLT_CODEX_PATH`, or a poisoned `PATH` is outside a safe setup. Install Codex from a trusted source.
- The Codex helper handles authentication and may load its own user configuration. Its implementation and services are separate dependencies, not audited by this project.
- CLT sends initialization and rate-limit-read requests only. It rejects unsolicited server requests rather than approving tools, prompts, or account changes.
- The taskbar widget uses Windows window ownership, event hooks, and a layered window. It does not inject code into Explorer or open a remote control endpoint.
- Named single-instance IPC is local to the Windows session and only requests opening the panel. It does not accept arbitrary commands or file paths.

## Release practices

Release builds omit debug symbols. Packages are assembled from an explicit allowlist, and include SHA-256 checksums. GitHub Actions use commit-pinned actions and read-only permissions for verification. Only the release job (a version tag or an explicit `[release]` commit on `main`) receives publishing and build-attestation permissions. Pull-request builds do not receive them.

The initial Windows release is **unsigned**. A matching checksum checks file integrity against the published manifest; it is not a substitute for a trusted publisher or independent security review.

Before publication, review the staged file list and commit history for secrets and personal data. `.gitignore`, automated tests, and secret scanning are defense layers, not guarantees of safety.
