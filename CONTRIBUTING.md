# Contributing

This page covers how to build, validate, and send changes. What the program does and how to install it are described
in the [README](README.md).

## Prerequisites

- The .NET 10 SDK. Published executables are Native AOT.
- Windows 11 24H2 or newer with PowerShell 7 (`pwsh`) for the Windows project and its scripts.
- WSL2 for the Linux relay. A Native AOT publish on Linux also needs `clang` and `zlib1g-dev` (or your
  distribution's equivalents).

## Project layout

The source is grouped by responsibility:

| Project                    | Responsibility                                                      |
| :------------------------- | :------------------------------------------------------------------ |
| `PinentryForWindows.Core`  | Assuan commands and contracts; no Windows API dependency.           |
| `PinentryForWindows`       | Windows executable: broker, cache, and UI. Publishes `pfw.exe`.     |
| `PinentryForWindows.Relay` | Linux connection and certificate validation. Publishes `pfw-relay`. |

The release workflow renames the two outputs to the architecture-suffixed assets listed in the README.

## Build and test on Windows

Run these from the repository root:

```powershell
dotnet restore PinentryForWindows.slnx
dotnet test --solution PinentryForWindows.slnx -c Debug
dotnet publish src/PinentryForWindows/PinentryForWindows.csproj -c Release -r win-x64
pwsh -NoProfile -File tests/Test-PinentryForWindows.ps1
pwsh -NoProfile -File tests/Test-InstallerConfig.ps1
pwsh -NoProfile -File tests/Test-BridgeSetup.ps1
```

The test runner is Microsoft.Testing.Platform, so `dotnet test` takes an explicit `--solution` or `--project`
argument, and VSTest filters do not apply.

The protocol script (`Test-PinentryForWindows.ps1`) defaults to non-interactive checks with an isolated cache under
`artifacts/`. Add `-Interactive -RequireWindowsHelloCache` for Windows Security and Hello checks. Add
`-IncludeGpgKeyTests -IncludeCacheTests -IncludeDecryptTest` to exercise a disposable, passphrase-protected GPG key in a
temporary GnuPG home; use only the script's dummy passphrase (`teste123` by default). The installer test uses a fake
`gpgconf` and never changes the primary agent configuration.

## Build and test the Linux relay

Test the Core and Relay projects separately in WSL2; the solution itself targets Windows:

```bash
dotnet test --project tests/PinentryForWindows.Core.Tests/PinentryForWindows.Core.Tests.csproj -c Debug
dotnet test --project tests/PinentryForWindows.Relay.Tests/PinentryForWindows.Relay.Tests.csproj -c Debug
dotnet publish src/PinentryForWindows.Relay/PinentryForWindows.Relay.csproj -c Release -r linux-x64
```

Use `linux-arm64` instead of `linux-x64` on ARM64. The WSL installer has an isolated check that uses a disposable
`HOME` and a fake GnuPG executable; it expects the `artifacts/` directory to exist:

```bash
mkdir -p artifacts
bash tests/Test-InstallerWsl.sh
```

## What automated tests do not cover

Native Windows Security, Windows Hello, GnuPG, and Hyper-V/vsock need real interactive validation on Windows 11 and
WSL2 in addition to the unit tests. If a change touches those paths, say in the pull request what you exercised by
hand and what you did not.

The release workflow runs the tests and a published-binary smoke check on each native x64 and ARM64 runner before
creating a release. ARM64 remains unverified until those GitHub jobs run.

## Measuring prompt latency

To measure the local `GETPIN` dispatch-to-Windows Security API interval, run
`pwsh -NoProfile -File tests/Measure-PinentryLatency.ps1 -Samples 21` with a published Windows x64 AOT binary available
under `artifacts/release-check-win-x64/`. Cancel every dialog; no password is required. The script reports the first
sample separately, median/p95 over the remaining prompts, and helper allocation/memory samples, then removes its
numeric-only measurement files.

Normal pinentry runs write no timing data; `PFW_LATENCY_LOG` and `PFW_HELPER_METRICS_LOG` enable the respective
diagnostics explicitly with absolute output paths. The latency marker is captured immediately before the native
credential prompt call and written after the dialog returns, so the user's response time is excluded. Helper
allocation is managed allocation during one worker invocation; peak working set and private memory are process-level
measurements.

## Repository conventions

- **Warnings are errors.** `Directory.Build.props` treats compiler and analyzer warnings as errors and applies
  `Meziantou.Analyzer` to every project.
- **Package versions are central.** Put versions in `Directory.Packages.props`, not on a project's `PackageReference`.
  Packages restore from nuget.org only (`NuGet.config`).
- **Code style comes from `.editorconfig`**, including its ReSharper options: two-space indentation and same-line
  opening braces. After changing C# files, run the ReSharper command-line tools and review both the cleanup diff and
  the inspection report:

  ```powershell
  jb cleanupcode --profile="Built-in: Full Cleanup" PinentryForWindows.slnx
  jb inspectcode PinentryForWindows.slnx --stdout --format=Text
  ```

- **Interop is generated.** CsWin32 generates Windows interop from `src/PinentryForWindows/NativeMethods.txt`; edit
  that input, not the generated files under `obj/`.
- **Diagnostics stay redacted.** Protocol stdout carries Assuan only. Never log passphrases, Assuan payloads, key
  descriptions or grips, or certificate contents.
- **Documentation is in English.** Do not state a validation that was not run.

## Sending changes

- `develop` is the integration branch and `main` holds releases. Branch from `develop` and open pull requests against
  it.
- Write commit messages in the [Conventional Commits](https://www.conventionalcommits.org/) format, for example
  `fix(cache): ...` or `docs(readme): ...`. The changelog is generated from them.
- Add or update tests with a behavior change, and keep the commands above passing.
