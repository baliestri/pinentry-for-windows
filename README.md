# Pinentry for Windows

Use the familiar **Windows Security** window to enter GnuPG passphrases on Windows or in WSL2. An optional, per-key
cache uses **Windows Hello** before returning a saved passphrase. Your private keys and `gpg-agent` stay where they are;
the WSL relay forwards only the pinentry conversation to the Windows desktop.

> [!NOTE]
> These instructions describe the current rewrite. Use a release that contains `pfw.x64.exe` / `pfw.arm64.exe` and
> `pfw-relay-x64` / `pfw-relay-arm64`. The older v1.2.1 release does not contain this Windows/WSL package; until a new
> release is published, [build from source](#build-and-test) to try it.

## What it looks like

| Signing request from Windows                                                                                                               | Signing request from WSL2 (`archlinux`)                                                                                                           |
|:-------------------------------------------------------------------------------------------------------------------------------------------|:--------------------------------------------------------------------------------------------------------------------------------------------------|
| <img src=".github/images/windows-security-windows.png" alt="Windows Security passphrase prompt for a Windows signing request" width="420"> | <img src=".github/images/windows-security-wsl.png" alt="Windows Security passphrase prompt labeled Pinentry for Windows [archlinux]" width="420"> |

Both are real Windows Security windows opened with **fictitious signing requests** for these screenshots. The WSL title
shows the paired distribution; there is no editable username field. You never need to launch a Windows `.exe` from WSL.

## Requirements

- Windows 11 24H2 or newer, signed in to an interactive desktop session.
- GnuPG on Windows, WSL2, or both, depending on where your keys live.
- PowerShell 7 (`pwsh`) for the Windows installer and broker setup commands shown below. The WSL installer needs Bash,
  `curl`, `sha256sum`, and `gpgconf`.
- For WSL2: a running distribution and permission to register the broker's Hyper-V service **once** in an elevated
  Windows Command Prompt. Normal prompts do not need elevation.
- For optional persistent cache: Windows Hello with a platform WebAuthn credential that supports PRF. Passphrase entry
  still works when this is unavailable.
- For a source build only: the .NET 10 SDK. Release executables are Native AOT.

## Choose where your key lives

| Your GnuPG key is in | Configure this `pinentry-program`    | The prompt appears on                              |
|:---------------------|:-------------------------------------|:---------------------------------------------------|
| Windows              | `pfw.x64.exe` or `pfw.arm64.exe`     | Windows desktop                                    |
| WSL2                 | `pfw-relay-x64` or `pfw-relay-arm64` | Windows desktop, labeled with the WSL distribution |

For WSL2, install the Windows side first, then [pair the distribution](#add-a-wsl2-distribution). Pair each distribution
separately; Windows and WSL private keys are never copied between them.

## Install on Windows

Run from any directory in PowerShell 7:

```powershell
irm 'https://exec.baliestri.dev/pfw-setup.ps1' | iex
```

The shortcut URLs currently return HTTP 302 to the raw scripts on `develop`; the commands stay the same when the
shortcuts later point to `main`. `irm` follows that redirect automatically.

The installer chooses `pfw.x64.exe` or `pfw.arm64.exe`, verifies its release SHA-256 checksum, configures the
**Windows**
`gpg-agent.conf`, and restarts the Windows agent. If it finds another `pinentry-program`, it asks before replacing it.
The default installation directory is GnuPG's `bindir`, which may require running the installation shell with elevation;
the installed pinentry itself runs normally as your Windows user.

To check prerequisites without opening a prompt or creating a Windows Hello credential, run the installed binary:

```powershell
& 'C:\path\to\pfw.x64.exe' --check
```

Use the actual installed path and architecture. Once configured, signing with an existing Windows GnuPG key brings up
the **Pinentry for Windows** window. To pass `-Update`, `-NoRestartAgent`, or `-InstallDirectory <path>` to a streamed
script, invoke the fetched text as a scriptblock instead of `iex`:

```powershell
& ([scriptblock]::Create((irm 'https://exec.baliestri.dev/pfw-setup.ps1'))) -Update
```

<details>
<summary>Manual Windows configuration</summary>

Set this line in the Windows GnuPG home directory's `gpg-agent.conf`, then run `gpgconf --kill gpg-agent`:

```text
pinentry-program C:/path/to/pfw.x64.exe
```

</details>

## Add a WSL2 distribution

Do this only if your **private key lives in WSL2**. The Windows executable and the Linux relay must trust each other's
public certificates; the relay never receives the broker's private key.

1. **Set up the Windows broker** as your normal Windows user, after installing the executable:

   ```powershell
   irm 'https://exec.baliestri.dev/pfw-setup-bridge.ps1' | iex
   ```

   The script finds the executable for your architecture in GnuPG's `bindir`, decoding paths such as `C%3a\Users\...`.
   If you installed elsewhere, invoke the fetched script as a scriptblock with
   `-ExecutablePath 'C:\path\to\pfw.x64.exe'`.
   The script prints the location of public `broker.cer`, a command to start the broker **now**, and a one-time
   `reg add`
   command. Run **only that printed registration command** in an elevated Command Prompt. The setup script does not
   elevate itself. It also configures broker startup at your next Windows sign-in.

2. **Prepare the relay in the WSL distribution** that owns your GnuPG key:

   ```bash
   curl -fsSL https://exec.baliestri.dev/pfw-setup-wsl.sh | bash
   ```

   `wget -qO- https://exec.baliestri.dev/pfw-setup-wsl.sh | bash` is equivalent. Both follow the redirect; the
   installed script still needs `curl` to download the release. This first call prepares the Linux identity but does
   not activate `gpg-agent` until the Windows broker has approved it.

   The script verifies the download and creates a Linux-only private identity under
   `~/.config/PinentryForWindows/relay`. Note the printed path to the **public** `client.cer`.

3. **Approve the WSL client on Windows.** Replace `archlinux`, `YOUR_USER`, and the executable path with your actual
   values; the distribution name becomes the label on the Windows Security window:

   ```powershell
   & ([scriptblock]::Create((irm 'https://exec.baliestri.dev/pfw-setup-bridge.ps1'))) `
     -Distribution archlinux `
     -RelayCertificatePath '\\wsl.localhost\archlinux\home\YOUR_USER\.config\PinentryForWindows\relay\client.cer'
   ```

   For a nondefault Windows installation, also pass `-ExecutablePath` here.

4. **Start the broker for this session** with the command printed in step 1, in a separate Windows terminal, or sign out
   and back in to use the configured startup entry. Then activate the relay in WSL:

   ```bash
   curl -fsSL https://exec.baliestri.dev/pfw-setup-wsl.sh | \
     bash -s -- --broker-certificate /mnt/c/path/to/broker.cer --activate
   ```

   Substitute the mounted path of the `broker.cer` printed in step 1. `wget -qO-` can replace `curl -fsSL` here as well.
   If WSL's `gpg-agent.conf` already has another pinentry, inspect it first; pass `--replace` only if you want to change
   it. The script backs up the file before updating it. Signing with an existing **WSL** key now shows **Pinentry for
   Windows [archlinux]** on the Windows desktop.

Repeat steps 2–4 for each additional distribution. Its client certificate, display label, and cached entries remain
separate. WSL1 and noninteractive/headless Windows sessions are not supported.

<details>
<summary>Manual WSL configuration and file permissions</summary>

After pairing, add `pinentry-program /home/YOUR_USER/.local/bin/pfw-relay-x64` to that distribution's `gpg-agent.conf`
and run `gpgconf --kill gpg-agent`. Keep the relay identity on the Linux filesystem, with `0700` on its directory and
`0600` on its private key. A Windows-mounted drive may report broader permissions even after `chmod`, so the relay
refuses to use a key stored there.

</details>

## Sign and use the optional cache

Sign as usual with GnuPG. If it needs a passphrase, enter it in Windows Security; the window title identifies whether
the request came from Windows or from a paired WSL distribution. Your GnuPG private key stays in its original keyring.

For a **software key**, Windows Security may show **Remember me**. It appears only when `gpg-agent` allows external
caching and supplies a stable key identity. Check it if you want this passphrase saved; Windows Hello protects the
initial device credential and asks for a fresh verification whenever the persistent cache is read. The two windows
below are from that **first-time setup** (later reads also require Hello):

| Create the protected credential                                                                                                                        | Confirm with Windows Hello                                                                                                                                 |
|:-------------------------------------------------------------------------------------------------------------------------------------------------------|:-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| <img src=".github/images/windows-hello-setup.png" alt="Windows Security offering to save the Pinentry for Windows passkey on this device" width="400"> | <img src=".github/images/windows-hello.png" alt="Windows Hello asking for a PIN while creating the protected Pinentry for Windows credential" width="400"> |

If Hello is cancelled or unavailable, enter the passphrase in Windows Security instead. **Smart-card and hardware-token
PINs are never offered this persistent cache**; the card, `gpg-agent`, and `scdaemon` manage those PINs. Also note that
`gpg-agent` can temporarily reuse a passphrase from its **own** in-memory cache, so a repeat signature may show no
pinentry window at all.

To remove the optional persistent cache credential and all its entries, run:

```powershell
& 'C:\path\to\pfw.x64.exe' --clear-cache
```

## Troubleshooting

| What you see                     | What to check                                                                                                                                                                                                            |
|:---------------------------------|:-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| No prompt on Windows             | Run `pfw.exe --check` using your installed path, check the Windows `gpg-agent.conf`, then restart that agent with `gpgconf --kill gpg-agent`.                                                                            |
| No WSL prompt                    | Start the broker in your Windows session, confirm the one-time Hyper-V service registration and WSL certificate pairing, and activate the relay in that distribution. Restart its agent with `gpgconf --kill gpg-agent`. |
| Relay refuses its private key    | Keep `~/.config/PinentryForWindows/relay` on the **Linux** filesystem, not under `/mnt/c`; its directory needs `0700` and its private key `0600`.                                                                        |
| No **Remember me** box           | Card PINs and requests without external-cache permission or a stable software-key identity are not eligible. Normal passphrase entry still works.                                                                        |
| Hello cancelled or not available | Type the passphrase normally. `--check` reports prerequisites but does not prove that a PRF assertion succeeds on this device.                                                                                           |
| Second signature shows no window | `gpg-agent` may already have the passphrase in its own short-lived cache; it did not necessarily access the persistent cache.                                                                                            |

Set `PFW_VERBOSE=1` before starting the affected pinentry, broker, or relay for fixed-category diagnostics on stderr.
Windows and WSL each read their **own** environment; restart the corresponding `gpg-agent` after changing it. Extra
events never include passphrases, key descriptions, grips, or certificates.

## Uninstall

On Windows, pass `-Uninstall` to the streamed setup script:

```powershell
& ([scriptblock]::Create((irm 'https://exec.baliestri.dev/pfw-setup.ps1'))) -Uninstall
```

It removes only its own GnuPG setting, clears the persistent cache and associated broker material, and removes its own
HKCU startup entry before deleting the binary.
Use `-DataDirectory` or `-BrokerDirectory` if you changed their defaults. It does **not** remove the HKLM Hyper-V
service
registration or stop a broker already running; remove the registration with elevation when no installation uses it.

For WSL, first remove the agent setting and relay executable while preserving the public `client.cer`:

```bash
curl -fsSL https://exec.baliestri.dev/pfw-setup-wsl.sh | bash -s -- --uninstall
```

Revoke that certificate on Windows with `pfw.exe --revoke-client <broker-directory> <Windows path to client.cer>`, then
remove the Linux identity:

```bash
curl -fsSL https://exec.baliestri.dev/pfw-setup-wsl.sh | bash -s -- --uninstall --revoked-on-windows
```

Use the confirmation flag only after revocation (or after the Windows broker identity has itself been removed).

## Technical reference

<details>
<summary>Command-line interface, cache details, and Assuan protocol</summary>

### How the bridge works

The Windows executable serves the local `gpg-agent` over binary stdin/stdout. In WSL2, a Linux relay sends the same
Assuan bytes over `AF_VSOCK` to a Windows broker on `AF_HYPERV`. TLS 1.3 authenticates both sides using separately
paired
certificates; an unpaired client is rejected before the Assuan greeting. The broker follows the current WSL VM ID after
a restart, while the paired certificate (not that VM ID) identifies the distribution. There is no implicit TCP fallback.
The relay writes only Assuan to stdout; redacted errors and opt-in diagnostics go to stderr.

### Command-line interface

The Windows binary accepts these commands (paths are local to Windows):

| Invocation                                                              | Purpose                                                                                              |
|-------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------|
| `pfw.exe`                                                               | Serve one local Assuan pinentry session on stdin/stdout.                                             |
| `pfw.exe --help` or `-h`                                                | Show command usage.                                                                                  |
| `pfw.exe --check`                                                       | Report local prerequisites and GnuPG configuration without enrolling a Hello credential.             |
| `pfw.exe --clear-cache`                                                 | Remove the persistent cache credential and all entries from the current data directory.              |
| `pfw.exe --clear-cache <40-hex-keygrip>`                                | Remove only the Windows-origin entry for a software key; `n/`, `s/`, and `u/` prefixes are accepted. |
| `pfw.exe --clear-cache <data-directory>`                                | Clear a cache in an explicit data directory.                                                         |
| `pfw.exe --init-broker <broker-directory>`                              | Create or load the Windows broker identity and print its public certificate path.                    |
| `pfw.exe --approve-client <broker-directory> <client.cer> wsl:<distro>` | Pair a public WSL client certificate with a distribution label.                                      |
| `pfw.exe --revoke-client <broker-directory> <client.cer>`               | Revoke the matching client certificate.                                                              |
| `pfw.exe --reset-broker <broker-directory>`                             | Remove local broker identity and paired clients.                                                     |
| `pfw.exe --vm-id <distribution>`                                        | Report the running WSL VM ID, or `not running`.                                                      |
| `pfw.exe --broker <broker-directory>`                                   | Run the Windows broker in the interactive user session.                                              |

`--ui-helper` is an internal, per-prompt process mode; it is not a user command. The Linux binary accepts:

| Invocation                                                | Purpose                                                            |
|-----------------------------------------------------------|--------------------------------------------------------------------|
| `pfw-relay`                                               | Serve the WSL pinentry using `~/.config/PinentryForWindows/relay`. |
| `pfw-relay --run <relay-directory>`                       | Serve using an explicit relay identity directory.                  |
| `pfw-relay --init <relay-directory>`                      | Create the client key and public `client.cer`.                     |
| `pfw-relay --trust-broker <relay-directory> <broker.cer>` | Pin the Windows broker's public certificate.                       |
| `pfw-relay --remove-identity <relay-directory>`           | Remove the local client identity and pinned broker certificate.    |

For opt-in diagnostics, set `PFW_VERBOSE=1` **before starting** the Windows pinentry/broker or the WSL relay. Each side
reads its own environment; setting the variable in Windows does not turn it on inside WSL. If `gpg-agent` was already
running, restart that environment's agent so its next pinentry child inherits the variable. Extra events go only to
stderr and contain fixed lifecycle/cache categories, never Assuan payloads, passphrases, key descriptions, grips, or
certificate contents. With the variable unset (or any value other than `1`), these extra events are disabled; normal
error categories still appear on stderr.

```powershell
$env:PFW_VERBOSE = '1' # Windows shell starting pfw.exe or its broker
```

```bash
export PFW_VERBOSE=1 # WSL shell starting pfw-relay or its gpg-agent
```

### Cache details and hardware tokens

Persistent cache is opt-in **per passphrase**. `gpg-agent` must send `OPTION allow-external-password-cache`, a stable
software-key identity must be available, and the user must check the save box in Windows Security. Each later cache hit
requests a fresh Windows Hello verification. A protected platform WebAuthn credential releases a PRF-derived encryption
key; entries are authenticated and encrypted with AES-GCM, partitioned by Windows/paired-WSL origin and key identity.
`S PASSWORD_FROM_CACHE` is sent only after a successful Hello-protected retrieval.

If Hello is cancelled, unavailable, or fails to return a usable PRF, the user enters the passphrase normally. There is
no silent fallback to an independently decryptable DPAPI cache. `SETERROR` bypasses cache for the next attempt;
`CLEARPASSPHRASE` removes the matching origin's entry. Old `pfwv2:` PasswordVault entries are not imported.

**Smart cards and hardware tokens never use this cache.** `SETKEYINFO --clear` and card/token prompts disable read and
write, even when external caching is otherwise allowed. Their PIN handling remains with the token, `gpg-agent`, and
`scdaemon`.

Use `--check` for noninteractive diagnostics. It reports Windows Hello platform-authenticator prerequisites, but does
not enroll a credential or claim that PRF has been proven without an actual assertion. To revoke the local cache
credential and remove all entries, run `--clear-cache` without arguments; the default data directory is
`%LOCALAPPDATA%\PinentryForWindows`. The directory form is useful for a separate test installation:

```powershell
& 'C:\path\to\pfw.x64.exe' --check
& 'C:\path\to\pfw.x64.exe' --clear-cache
& 'C:\path\to\pfw.x64.exe' --clear-cache "$env:LOCALAPPDATA\PinentryForWindows"
```

`--clear-cache <40-hex-keygrip>` (also accepting `n/`, `s/`, or `u/` prefixes) removes only the matching **local
Windows** key entry. It does not clear a paired WSL origin; use `CLEARPASSPHRASE` within that origin or clear all
entries explicitly.

Buffers owned by this application are bounded and wiped after use. Windows credential-provider memory, OS transports,
runtime copies, swap, and crash dumps cannot all be erased by the application. A process running as the same user can
still request an additional Hello operation; users should reject unexpected verification prompts.

### Pinentry protocol behavior

| Assuan command                                 | Behavior                                                                                                                                                     |
|------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `GETPIN`                                       | Show Windows Security and return the passphrase, or a cancellation/timeout error.                                                                            |
| `CONFIRM` / `CONFIRM --one-button` / `MESSAGE` | Show a native confirmation or information dialog.                                                                                                            |
| `GETINFO version\|pid\|flavor\|ttyinfo`        | Return executable/protocol metadata.                                                                                                                         |
| `SETTITLE`, `SETDESC`, `SETPROMPT`, `SETERROR` | Set per-session dialog text; the Windows Security title for `GETPIN` is fixed as described above, while `SETTITLE` still applies to `CONFIRM` and `MESSAGE`. |
| `SETREPEAT`, `SETREPEATERROR`, `SETTIMEOUT`    | Configure repeat entry and timeout; the repeat-error text is accepted as session metadata.                                                                   |
| `SETOK`, `SETCANCEL`, `SETNOTOK`               | Record button labels; Windows dialogs use the labels their native controls support.                                                                          |
| `SETKEYINFO`, `CLEARPASSPHRASE <keygrip>`      | Set/clear software-key cache identity or remove this origin's cached passphrase.                                                                             |
| `OPTION`, `RESET`, `BYE`, `NOP`, `HELP`        | Handled by AssuanLibrary; `RESET` clears transient state without changing the authenticated origin.                                                          |

Recognized `OPTION` values are `allow-external-password-cache`, `default-ok`, `default-cancel`, `default-notok`, and
`default-prompt`. `grab`, `no-grab`, and unknown options are accepted without changing the Windows credential window.
The relay forwards Assuan bytes without interpreting them; diagnostics never enter protocol stdout.
Local Windows sessions decode dialog text as UTF-8 first, then fall back to the active Windows ANSI code page for localized GnuPG labels. This supports agents that mix UTF-8 key descriptions with native-encoded prompts. WSL broker sessions keep UTF-8 decoding, and key identifiers never use the text fallback.

`SETKEYINFO --clear` disables cache eligibility for the current card/token request. A key with a stable 40-hex-digit
grip, optionally prefixed by `n/`, `s/`, or `u/`, may be eligible for software-key caching. `SETERROR` suppresses one
cache read so a rejected passphrase is not immediately reused. Successful repeat-passphrase input sends
`S PIN_REPEATED`; a successful Hello-protected cache read sends `S PASSWORD_FROM_CACHE`. Responses use Assuan's bounded
binary-safe data escaping, and cancellation, timeout, and repeat mismatch return protocol errors rather than a partial
password.

</details>

## Build and test

```powershell
dotnet restore PinentryForWindows.slnx
dotnet test PinentryForWindows.slnx -c Debug
dotnet publish src/PinentryForWindows/PinentryForWindows.csproj -c Release -r win-x64
pwsh -NoProfile -File tests/Test-PinentryForWindows.ps1
pwsh -NoProfile -File tests/Test-InstallerConfig.ps1
pwsh -NoProfile -File tests/Test-BridgeSetup.ps1
```

The WSL installer has an isolated uninstall check that uses a disposable HOME and fake GnuPG executable:

```bash
bash tests/Test-InstallerWsl.sh
```

The protocol script defaults to non-interactive checks with an isolated cache under `artifacts/`. Add
`-Interactive -RequireWindowsHelloCache` for Windows Security and Hello checks. Add
`-IncludeGpgKeyTests -IncludeCacheTests -IncludeDecryptTest` to exercise a disposable, passphrase-protected GPG key in a
temporary GnuPG home; use only the script's dummy passphrase (`teste123` by default). The installer test uses a fake
`gpgconf` and never changes the primary agent configuration.

To measure the local `GETPIN` dispatch-to-Windows Security API interval, run
`pwsh -NoProfile -File tests/Measure-PinentryLatency.ps1 -Samples 21` with a published Windows x64 AOT binary available
under `artifacts/release-check-win-x64/`. Cancel every dialog; no password is required. The script reports the first
sample separately, median/p95 over the remaining prompts, and helper allocation/memory samples, then removes its
numeric-only measurement files. Normal pinentry runs write no timing data; `PFW_LATENCY_LOG` and
`PFW_HELPER_METRICS_LOG` enable the respective diagnostics explicitly with absolute output paths. The latency marker is
captured immediately before the native credential prompt call and written after the dialog returns, so the user's
response time is excluded. Helper allocation is managed allocation during one worker invocation; peak working set and
private memory are process-level measurements.

Build and test the Linux relay in WSL2:

```bash
dotnet test tests/PinentryForWindows.Core.Tests/PinentryForWindows.Core.Tests.csproj -c Debug
dotnet test tests/PinentryForWindows.Relay.Tests/PinentryForWindows.Relay.Tests.csproj -c Debug
dotnet publish src/PinentryForWindows.Relay/PinentryForWindows.Relay.csproj -c Release -r linux-x64
```

The published project outputs are `pfw.exe` and `pfw-relay`; the release workflow stages them as the
architecture-suffixed assets listed above. Native Windows Security, Windows Hello, GnuPG and Hyper-V/vsock require real
interactive Windows 11/WSL2 validation in addition to unit tests.
The release workflow also runs tests and a published-binary smoke check on each native x64/ARM64 runner before creating
a release; ARM64 remains unverified until those GitHub jobs run.

Until a release contains the assets listed at the top of this README, the install scripts cannot download this rewrite.
Use the published Windows `pfw.exe` with the [manual Windows configuration](#install-on-windows); for WSL, publish the
relay in the distribution, run its `--init` and `--trust-broker` commands from
the [CLI reference](#technical-reference),
approve its public certificate with `setup-bridge.ps1`, and set the relay's published path as WSL's `pinentry-program`.
The Windows broker still needs the one-time service registration described above.

The source is grouped by responsibility: `PinentryForWindows.Core` holds Assuan commands and contracts, the Windows
project handles broker/cache/UI, and `PinentryForWindows.Relay` handles the Linux connection and certificate validation.

## Acknowledgments

- [Ookii.VmSockets](https://github.com/SvenGroot/Ookii.VmSockets) by Sven Groot informed the minimal Hyper-V/VSock
  endpoint implementation. The NuGet package is **not** a runtime dependency.
  See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for its MIT notice.
- [AssuanLibrary](https://github.com/baliestri/assuan-library) 2.0.0 supplies the Assuan protocol primitives and
  server-session API.

Pinentry for Windows is licensed under the [MIT License](LICENSE.md).
