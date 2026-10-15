# Contributing

Thanks for helping make the counter a little less distracting.

1. Use Windows and the .NET 10 SDK.
2. Create a focused branch and keep changes small.
3. Build with `./build.ps1` or `dotnet build -c Release -r win-x64`.
4. Run `--self-test` and inspect any affected panel/widget layout.
5. Explain the user-visible problem, resulting behavior, and validation in your pull request.

Tests and `--demo-previews` do not require a Codex login. Live connection tests do; do not add account credentials or assume access to a maintainer's session in CI.

## Screenshots and issues

Use demo data when possible. Remove account details, personal file paths, tokens, and unrelated desktop content. Never attach `auth.json` or the `.codex` folder.

## Areas that could use help

- English and additional UI languages. The current UI is Turkish.
- Keyboard access and screen-reader coverage.
- More display scaling, multi-monitor, and taskbar-layout testing.
- More resilient Codex installation discovery.

## Project conventions

- Keep Codex communication read-only; do not silently introduce model turns or tool approval.
- Use process argument lists, never shell-built strings for Codex invocation.
- Keep preferences separate from account/authentication data.
- Avoid new dependencies unless they solve a concrete problem.
- Match the small, quiet taskbar presentation.

Contributions are provided under the repository's MIT license. For security concerns, follow [SECURITY.md](SECURITY.md) instead of posting exploit details publicly.
