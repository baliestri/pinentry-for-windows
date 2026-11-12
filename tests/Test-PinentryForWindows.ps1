<#
.SYNOPSIS
Runs protocol, option, interactive PIN, cache, and optional GPG smoke tests for PinentryForWindows.

.DESCRIPTION
The default run avoids creating GPG keys. It drives pfw.exe directly over
Assuan with a unique, disposable cache directory under repository artifacts.

Use -Interactive to run GETPIN/CredUI/Windows Hello scenarios.
Use -RequireWindowsHelloCache to fail when a cache hit falls back to a normal prompt.
Use -IncludeDialogTests to run manual MESSAGE/CONFIRM dialog checks.
Use -IncludeCancelTests to run manual cancellation checks.
Use -IncludeGpgKeyTests to create a throwaway GNUPGHOME and a temporary key; the
script deletes generated keys and removes the temporary GNUPGHOME in cleanup.
Use -ProvisionGpgOnly with -IncludeGpgKeyTests to verify disposable key creation
and cleanup without opening a Windows Security prompt.
Use -IncludeDecryptTest (requires -IncludeGpgKeyTests) to add an encryption subkey and
run a live encrypt + decrypt scenario. The user must enter the PIN when prompted.
Use -IncludeCacheTests (requires -IncludeGpgKeyTests) to run gpg-agent internal cache-hit
(no PIN prompt expected) and cache-clear (PIN re-prompted) scenarios. Sets
default-cache-ttl to 300 seconds instead of 1 so the cache survives the test sequence.
Use -GpgCacheHitOnly to create a disposable key, sign once to prime gpg-agent,
then repeat the signature for visual cache-hit verification; no other tests run.
Use -CardStatusOnly for a read-only OpenPGP smart-card status query with an
isolated temporary GNUPGHOME; no PIN or key creation is requested.
Use -CardSignOnly to sign a disposable message with a signing key already on
the card. It may ask for the real card PIN; no card key is created or changed.
Only use a disposable test passphrase. Test cache credentials are removed in cleanup.
#>

[CmdletBinding()]
param(
  [string] $Configuration = 'Debug',
  [string] $Runtime = 'win-x64',
  [string] $ExpectedPin = 'teste123',
  [int] $TimeoutSeconds = 180,
  [switch] $NoBuild,
  [switch] $Restore,
  [switch] $Interactive,
  [switch] $RequireWindowsHelloCache,
  [switch] $IncludeDialogTests,
  [switch] $IncludeCancelTests,
  [switch] $IncludeGpgKeyTests,
  [switch] $ProvisionGpgOnly,
  [switch] $IncludeDecryptTest,
  [switch] $IncludeCacheTests,
  [switch] $GpgCacheHitOnly,
  [switch] $CardStatusOnly,
  [switch] $CardSignOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Script:RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$Script:SlnPath = Join-Path $Script:RepoRoot 'PinentryForWindows.slnx'
$Script:ProjectDir = Join-Path $Script:RepoRoot 'src' 'PinentryForWindows'
$Script:RunId = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$Script:TestDataDirectory = Join-Path $Script:RepoRoot 'artifacts' "pinentry-test-$Script:RunId"
$Script:CreatedGpgHome = $null
$Script:CreatedGpgRootLink = $null
$Script:CreatedSubstDrive = $null
$Script:CreatedSubstBackingDir = $null
$Script:ShortTempRoot = $null
$Script:CreatedGpgFingerprints = New-Object 'System.Collections.Generic.List[string]'
$Script:Failures = New-Object 'System.Collections.Generic.List[string]'
$Script:Warnings = New-Object 'System.Collections.Generic.List[string]'
$Script:Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Write-Step {
  param([string] $Message)
  Write-Host ""
  Write-Host "== $Message" -ForegroundColor Cyan
}

function Write-Case {
  param([string] $Message)
  Write-Host " - $Message" -ForegroundColor Gray
}

function Add-Failure {
  param([string] $Message)
  [void] $Script:Failures.Add($Message)
  Write-Host "   FAIL: $Message" -ForegroundColor Red
}

function Add-Warning {
  param([string] $Message)
  [void] $Script:Warnings.Add($Message)
  Write-Host "   WARN: $Message" -ForegroundColor Yellow
}

function Assert-True {
  param(
    [bool] $Condition,
    [string] $Message
  )

  if (-not $Condition) {
    throw $Message
  }
}

function Invoke-CheckedCommand {
  param(
    [string] $FilePath,
    [string[]] $Arguments,
    [string] $Description,
    [string] $WorkingDirectory = $Script:RepoRoot,
    [hashtable] $Environment = @{ },
    [string] $StandardInput = $null
  )

  Write-Case $Description

  $psi = [System.Diagnostics.ProcessStartInfo]::new()
  $psi.FileName = $FilePath
  $psi.Arguments = Join-ProcessArguments -Arguments $Arguments
  $psi.WorkingDirectory = $WorkingDirectory
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.RedirectStandardInput = $null -ne $StandardInput
  Set-ProcessEncoding -StartInfo $psi

  foreach ($entry in $Environment.GetEnumerator()) {
    $psi.Environment[$entry.Key] = [string]$entry.Value
  }

  $process = [System.Diagnostics.Process]::Start($psi)
  try {
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if ($null -ne $StandardInput) {
      $bytes = $Script:Utf8NoBom.GetBytes($StandardInput + "`n")
      try {
        $process.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
        $process.StandardInput.BaseStream.Flush()
      }
      finally {
        [System.Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes)
        $process.StandardInput.Close()
      }
    }

    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
      try {
        $process.Kill($true)
      }
      catch {
        $process.Kill()
      }
      throw "$Description timed out after $TimeoutSeconds seconds."
    }

    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) {
      throw @"
$Description failed with exit code $( $process.ExitCode ).
STDOUT:
$stdout
STDERR:
$stderr
"@
    }

    [pscustomobject]@{
      ExitCode = $process.ExitCode
      StdOut = $stdout
      StdErr = $stderr
    }
  }
  finally {
    if (-not $process.HasExited) {
      try {
        $process.Kill($true)
      }
      catch {
        $process.Kill()
      }
    }
    $process.Dispose()
  }
}

function Join-ProcessArguments {
  param([string[]] $Arguments)

  ($Arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' '
}

function ConvertTo-ProcessArgument {
  param([string] $Argument)

  if ($Argument -notmatch '[\s"]') {
    return $Argument
  }

  $builder = [System.Text.StringBuilder]::new()
  [void] $builder.Append('"')

  $backslashes = 0
  foreach ($char in $Argument.ToCharArray()) {
    if ($char -eq '\') {
      $backslashes++
      continue
    }

    if ($char -eq '"') {
      [void] $builder.Append(('\' * (($backslashes * 2) + 1)))
      [void] $builder.Append('"')
      $backslashes = 0
      continue
    }

    if ($backslashes -gt 0) {
      [void] $builder.Append(('\' * $backslashes))
      $backslashes = 0
    }

    [void] $builder.Append($char)
  }

  if ($backslashes -gt 0) {
    [void] $builder.Append(('\' * ($backslashes * 2)))
  }

  [void] $builder.Append('"')
  $builder.ToString()
}

function Get-PinentryExecutable {
  $expected = Join-Path $Script:ProjectDir "bin\$Configuration\net10.0-windows10.0.26100.0\$Runtime\pfw.exe"
  if (Test-Path $expected) {
    return (Resolve-Path $expected).Path
  }

  $candidate = Get-ChildItem -Path (Join-Path $Script:ProjectDir 'bin') -Recurse -Filter 'pfw.exe' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\$([Regex]::Escape($Configuration) )\\" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

  if ($null -eq $candidate) {
    throw "pfw.exe was not found. Run the script without -NoBuild, or build the project first."
  }

  $candidate.FullName
}

function Build-Pinentry {
  if ($NoBuild) {
    Write-Step 'Skipping build'
    return
  }

  Write-Step 'Building PinentryForWindows'

  if ($Restore) {
    [void](Invoke-CheckedCommand -FilePath 'dotnet' -Arguments @('restore', $Script:SlnPath) -Description 'dotnet restore')
  }

  [void](Invoke-CheckedCommand -FilePath 'dotnet' -Arguments @('build', $Script:SlnPath, '--configuration', $Configuration, '--no-restore') -Description 'dotnet build --no-restore')
}

function Start-PinentrySession {
  param([string] $ExecutablePath)

  $psi = [System.Diagnostics.ProcessStartInfo]::new()
  $psi.FileName = $ExecutablePath
  $psi.WorkingDirectory = Split-Path $ExecutablePath -Parent
  $psi.UseShellExecute = $false
  $psi.RedirectStandardInput = $true
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.CreateNoWindow = $false
  $psi.Environment['PFW_DATA_DIR'] = $Script:TestDataDirectory
  Set-ProcessEncoding -StartInfo $psi

  $process = [System.Diagnostics.Process]::Start($psi)

  $session = [pscustomobject]@{
    Process = $process
    Input = $process.StandardInput
    Output = $process.StandardOutput
  }

  $banner = Read-AssuanResponse -Session $session -TimeoutSeconds $TimeoutSeconds
  Assert-True ($banner.Terminal -match '^OK\b') "Pinentry did not start correctly. Response: $( $banner.Lines -join ' | ' )"

  # Windows PowerShell can emit a UTF-8 BOM on the first redirected stdin write.
  # Send one ignored command so real test commands are not prefixed with it.
  [void](Send-AssuanCommand -Session $session -Command 'NOP' -TimeoutSeconds 5)

  $session
}

function Stop-PinentrySession {
  param([pscustomobject] $Session)

  if ($null -eq $Session -or $null -eq $Session.Process) {
    return
  }

  try {
    if (-not $Session.Process.HasExited) {
      try {
        [void](Send-AssuanCommand -Session $Session -Command 'BYE' -TimeoutSeconds 5)
      }
      catch {
      }
    }
  }
  finally {
    if (-not $Session.Process.HasExited) {
      try {
        $Session.Process.Kill($true)
      }
      catch {
        try {
          if (-not $Session.Process.HasExited) {
            $Session.Process.Kill()
          }
        }
        catch {
        }
      }
    }

    $Session.Process.Dispose()
  }
}

function Set-ProcessEncoding {
  param([System.Diagnostics.ProcessStartInfo] $StartInfo)

  $properties = $StartInfo.GetType().GetProperties().Name
  if ($StartInfo.RedirectStandardInput -and $properties -contains 'StandardInputEncoding') {
    $StartInfo.StandardInputEncoding = $Script:Utf8NoBom
  }

  if ($StartInfo.RedirectStandardOutput -and $properties -contains 'StandardOutputEncoding') {
    $StartInfo.StandardOutputEncoding = $Script:Utf8NoBom
  }

  if ($StartInfo.RedirectStandardError -and $properties -contains 'StandardErrorEncoding') {
    $StartInfo.StandardErrorEncoding = $Script:Utf8NoBom
  }
}

function Read-AssuanResponse {
  param(
    [pscustomobject] $Session,
    [int] $TimeoutSeconds = 60
  )

  $lines = New-Object 'System.Collections.Generic.List[string]'
  $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
  $readTask = $Session.Output.ReadLineAsync()

  while ([DateTimeOffset]::Now -lt $deadline) {
    if ($Session.Process.HasExited -and -not $readTask.IsCompleted) {
      throw "Pinentry exited before a complete Assuan response was received."
    }

    if (-not $readTask.Wait(250)) {
      continue
    }

    $line = $readTask.Result
    if ($null -eq $line) {
      throw "Pinentry closed stdout before a complete Assuan response was received."
    }

    $lines.Add($line)

    if ($line -match '^(OK|ERR)\b') {
      return [pscustomobject]@{
        Lines = [string[]]$lines
        Terminal = $line
      }
    }

    $readTask = $Session.Output.ReadLineAsync()
  }

  throw "Timed out after $TimeoutSeconds seconds waiting for Assuan response. Lines so far: $( $lines -join ' | ' )"
}

function Send-AssuanCommand {
  param(
    [pscustomobject] $Session,
    [string] $Command,
    [int] $TimeoutSeconds = 60
  )

  $bytes = $Script:Utf8NoBom.GetBytes($Command + "`n")
  $Session.Input.BaseStream.Write($bytes, 0, $bytes.Length)
  $Session.Input.BaseStream.Flush()
  Read-AssuanResponse -Session $Session -TimeoutSeconds $TimeoutSeconds
}

function Assert-AssuanResponse {
  param(
    [pscustomobject] $Response,
    [string] $Command,
    [string] $TerminalPattern = '^OK\b',
    [string[]] $RequiredPatterns = @(),
    [string[]] $ForbiddenPatterns = @()
  )

  $text = $Response.Lines -join "`n"
  Assert-True ($Response.Terminal -match $TerminalPattern) "Command '$Command' returned '$( $Response.Terminal )', expected '$TerminalPattern'. Full response: $text"

  foreach ($pattern in $RequiredPatterns) {
    Assert-True ($text -match $pattern) "Command '$Command' response did not match required pattern '$pattern'. Full response: $text"
  }

  foreach ($pattern in $ForbiddenPatterns) {
    Assert-True ($text -notmatch $pattern) "Command '$Command' response matched forbidden pattern '$pattern'. Full response: $text"
  }
}

function Invoke-AssuanScenario {
  param(
    [string] $Name,
    [string] $ExecutablePath,
    [object[]] $Steps,
    [switch] $AllowUi
  )

  Write-Case $Name
  $session = $null
  $passed = $false

  try {
    $session = Start-PinentrySession -ExecutablePath $ExecutablePath

    foreach ($step in $Steps) {
      if ($step.Instruction) {
        Write-Host "   ACTION: $( $step.Instruction )" -ForegroundColor Yellow
      }

      $response = Send-AssuanCommand -Session $session -Command $step.Command -TimeoutSeconds $TimeoutSeconds
      Assert-AssuanResponse `
        -Response $response `
        -Command $step.Command `
        -TerminalPattern $( if ($null -ne $step.TerminalPattern) {
        $step.TerminalPattern
      }
      else {
        '^OK\b'
      } ) `
        -RequiredPatterns $( if ($null -ne $step.RequiredPatterns) {
        $step.RequiredPatterns
      }
      else {
        @()
      } ) `
        -ForbiddenPatterns $( if ($null -ne $step.ForbiddenPatterns) {
        $step.ForbiddenPatterns
      }
      else {
        @()
      } )
    }

    Write-Host "   PASS" -ForegroundColor Green
    $passed = $true
  }
  catch {
    Add-Failure "${Name}: $( $_.Exception.Message )"
  }
  finally {
    Stop-PinentrySession -Session $session
  }

  $passed
}

function New-Step {
  param(
    [string] $Command,
    [string] $TerminalPattern = '^OK\b',
    [string[]] $RequiredPatterns = @(),
    [string[]] $ForbiddenPatterns = @(),
    [string] $Instruction = ''
  )

  [pscustomobject]@{
    Command = $Command
    TerminalPattern = $TerminalPattern
    RequiredPatterns = $RequiredPatterns
    ForbiddenPatterns = $ForbiddenPatterns
    Instruction = $Instruction
  }
}

function Invoke-ProtocolAndOptionTests {
  param([string] $ExecutablePath)

  Write-Step 'Protocol and option coverage'
  $testGrip = '0123456789ABCDEF0123456789ABCDEF01234567'

  $basicSteps = @(
    New-Step 'GETINFO version' -RequiredPatterns @('(?m)^D .+')
    New-Step 'GETINFO pid' -RequiredPatterns @('(?m)^D \d+')
    New-Step 'GETINFO flavor' -RequiredPatterns @('(?m)^D pinentry-for-windows$')
    New-Step 'GETINFO ttyinfo' -RequiredPatterns @('(?m)^D CONOUT\$ w32$')
    New-Step 'GETINFO unsupported-value' -TerminalPattern '^ERR\b' -RequiredPatterns @('unknown value')
    New-Step 'OPTION allow-external-password-cache'
    New-Step 'OPTION default-ok=Unlock'
    New-Step 'OPTION default-cancel=Abort'
    New-Step 'OPTION default-notok=Reject'
    New-Step 'OPTION default-prompt=PIN:'
    New-Step 'OPTION grab'
    New-Step 'OPTION no-grab'
    New-Step 'OPTION unknown-option=ignored'
    New-Step 'SETTITLE Pinentry Test Window'
    New-Step 'SETPROMPT PIN:'
    New-Step 'SETOK _Unlock'
    New-Step 'SETCANCEL _Cancel'
    New-Step 'SETNOTOK _Reject'
    New-Step 'SETTIMEOUT 1'
    New-Step 'SETTIMEOUT invalid'
    New-Step 'SETKEYINFO protocol-key'
    New-Step 'SETKEYINFO --clear'
    New-Step "CLEARPASSPHRASE $testGrip"
    New-Step 'RESET'
  )

  [void](Invoke-AssuanScenario -Name 'basic commands and OPTION variants' -ExecutablePath $ExecutablePath -Steps $basicSteps)

  [void](Invoke-AssuanScenario -Name 'SETDESC plain text' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'SETDESC Plain description for prompt'
    New-Step 'SETKEYINFO desc-plain-key'
  ))

  [void](Invoke-AssuanScenario -Name 'SETDESC key id extraction' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'SETDESC Please unlock key ID ABCDEF1234567890'
    New-Step 'SETKEYINFO desc-keyid-key'
  ))

  [void](Invoke-AssuanScenario -Name 'SETDESC MAC extraction' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'SETDESC Device 00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF'
    New-Step 'SETKEYINFO desc-mac-key'
  ))

  [void](Invoke-AssuanScenario -Name 'SETDESC smart card holder/number extraction' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'SETDESC Please unlock the card%0A%0ANumber: 31 184 111%0AHolder: Bruno Sales'
    New-Step 'SETKEYINFO desc-card-key'
  ))

  [void](Invoke-AssuanScenario -Name 'invalid arity coverage' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'GETINFO' -TerminalPattern '^ERR\b'
    New-Step 'GETINFO a b' -TerminalPattern '^ERR\b'
    New-Step 'OPTION' -TerminalPattern '^ERR\b'
    New-Step 'SETTITLE' -TerminalPattern '^ERR\b'
    New-Step 'SETPROMPT' -TerminalPattern '^ERR\b'
    New-Step 'SETDESC' -TerminalPattern '^ERR\b'
    New-Step 'SETERROR' -TerminalPattern '^ERR\b'
    New-Step 'SETREPEAT' -TerminalPattern '^ERR\b'
    New-Step 'SETREPEATERROR' -TerminalPattern '^ERR\b'
    New-Step 'SETOK' -TerminalPattern '^ERR\b'
    New-Step 'SETCANCEL' -TerminalPattern '^ERR\b'
    New-Step 'SETNOTOK' -TerminalPattern '^ERR\b'
    New-Step 'SETKEYINFO' -TerminalPattern '^ERR\b'
    New-Step 'SETKEYINFO a b' -TerminalPattern '^ERR\b'
    New-Step 'CLEARPASSPHRASE' -TerminalPattern '^ERR\b'
    New-Step 'CLEARPASSPHRASE a b' -TerminalPattern '^ERR\b'
  ))
}

function Invoke-CacheHitOrFallbackScenario {
  param(
    [string] $ExecutablePath,
    [string] $CacheKey
  )

  $name = 'GETPIN cache hit gated by Windows Hello'
  Write-Case $name
  $session = $null
  $passed = $false

  try {
    $session = Start-PinentrySession -ExecutablePath $ExecutablePath

    foreach ($step in @(
      New-Step 'OPTION allow-external-password-cache'
      New-Step "SETKEYINFO $CacheKey"
      New-Step 'SETDESC Cache hit should ask Windows Hello first'
    )) {
      $response = Send-AssuanCommand -Session $session -Command $step.Command -TimeoutSeconds $TimeoutSeconds
      Assert-AssuanResponse -Response $response -Command $step.Command
    }

    Write-Host "   ACTION: Approve Windows Hello if it appears. If a normal PIN/password prompt appears, enter '$ExpectedPin'; that means cache lookup fell back to prompting." -ForegroundColor Yellow

    $response = Send-AssuanCommand -Session $session -Command 'GETPIN' -TimeoutSeconds $TimeoutSeconds
    Assert-AssuanResponse -Response $response -Command 'GETPIN' -RequiredPatterns @("(?m)^D $([Regex]::Escape($ExpectedPin) )$")

    $text = $response.Lines -join "`n"
    if ($text -match '(?m)^S PASSWORD_FROM_CACHE$') {
      Write-Host '   PASS' -ForegroundColor Green
    }
    elseif ($RequireWindowsHelloCache) {
      throw "GETPIN returned the expected PIN but did not report PASSWORD_FROM_CACHE. Windows Hello cache hit was required. Full response: $text"
    }
    else {
      Add-Warning 'GETPIN returned the expected PIN through a normal prompt instead of PASSWORD_FROM_CACHE. The save checkbox or Windows Hello PRF may be unavailable.'
      Write-Host '   PASS (manual fallback accepted)' -ForegroundColor Green
    }

    $passed = $true
  }
  catch {
    Add-Failure "${name}: $( $_.Exception.Message )"
  }
  finally {
    Stop-PinentrySession -Session $session
  }

  $passed
}

function Invoke-InteractivePinentryTests {
  param([string] $ExecutablePath)

  Write-Step 'Interactive GETPIN, CredUI, cache, and Windows Hello coverage'
  Write-Host "Use PIN '$ExpectedPin' whenever a PIN/password prompt appears." -ForegroundColor Yellow

  $suffix = $Script:RunId.ToUpperInvariant()
  $cacheKey = ('B' * 28) + $suffix
  $repeatKey = ('D' * 28) + $suffix

  [void](Invoke-AssuanScenario -Name 'GETPIN manual fallback/no cache' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'SETTITLE Manual PIN Test'
    New-Step 'SETDESC Manual prompt without external cache'
    New-Step 'GETPIN' -RequiredPatterns @("(?m)^D $([Regex]::Escape($ExpectedPin) )$") -Instruction "Enter '$ExpectedPin' in the CredUI prompt."
  ) -AllowUi)

  [void](Invoke-AssuanScenario -Name 'GETPIN external cache enabled without key info still prompts' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'OPTION allow-external-password-cache'
    New-Step 'SETDESC External cache is enabled, but no key info was set'
    New-Step 'GETPIN' -RequiredPatterns @("(?m)^D $([Regex]::Escape($ExpectedPin) )$") -ForbiddenPatterns @('(?m)^S PASSWORD_FROM_CACHE$') -Instruction "No cache key exists for this request. Enter '$ExpectedPin' in the CredUI prompt."
  ) -AllowUi)

  [void](Invoke-AssuanScenario -Name 'GETPIN software key cache opt-in' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'OPTION allow-external-password-cache'
    New-Step "SETKEYINFO $cacheKey"
    New-Step 'SETDESC Please unlock the OpenPGP secret key:%0A%22Disposable Test <test@example.invalid>%22%0AID ABCDEF1234567890'
    New-Step 'GETPIN' -RequiredPatterns @("(?m)^D $([Regex]::Escape($ExpectedPin) )$") -Instruction "Confirm there is no username field. Enter '$ExpectedPin', check Remember, and approve Windows Hello enrollment."
  ) -AllowUi)

  [void](Invoke-AssuanScenario -Name 'smart-card PIN never uses external cache' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'OPTION allow-external-password-cache'
    New-Step 'SETDESC Please unlock the card%0A%0ANumber: 31 184 111%0AHolder: Test User'
    New-Step 'SETKEYINFO --clear'
    New-Step 'GETPIN' -RequiredPatterns @("(?m)^D $([Regex]::Escape($ExpectedPin) )$") -ForbiddenPatterns @('(?m)^S PASSWORD_FROM_CACHE$') -Instruction "Do not expect a Remember checkbox. Enter '$ExpectedPin' as a disposable card PIN."
  ) -AllowUi)

  [void](Invoke-CacheHitOrFallbackScenario -ExecutablePath $ExecutablePath -CacheKey $cacheKey)

  [void](Invoke-AssuanScenario -Name 'GETPIN skips cache when SETERROR is present' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'OPTION allow-external-password-cache'
    New-Step "SETKEYINFO $cacheKey"
    New-Step 'SETERROR Bad PIN'
    New-Step 'GETPIN' -RequiredPatterns @("(?m)^D $([Regex]::Escape($ExpectedPin) )$") -ForbiddenPatterns @('(?m)^S PASSWORD_FROM_CACHE$') -Instruction "Because SETERROR is set, Windows Hello/cache must be skipped. Enter '$ExpectedPin' manually."
  ) -AllowUi)

  [void](Invoke-AssuanScenario -Name 'GETPIN repeat success' -ExecutablePath $ExecutablePath -Steps @(
    New-Step "SETKEYINFO $repeatKey"
    New-Step 'SETDESC Repeat PIN prompt'
    New-Step 'SETREPEAT Repeat PIN prompt'
    New-Step 'GETPIN' -RequiredPatterns @('(?m)^S PIN_REPEATED$', "(?m)^D $([Regex]::Escape($ExpectedPin) )$") -Instruction "Enter '$ExpectedPin' in both CredUI prompts. Both titles should be 'Pinentry for Windows'; the second description requests repetition."
  ) -AllowUi)

  [void](Invoke-AssuanScenario -Name 'GETPIN repeat mismatch' -ExecutablePath $ExecutablePath -Steps @(
    New-Step "SETKEYINFO $repeatKey"
    New-Step 'SETDESC Repeat mismatch prompt'
    New-Step 'SETREPEAT Repeat mismatch prompt'
    New-Step 'SETREPEATERROR Passphrases do not match'
    New-Step 'GETPIN' -TerminalPattern '^ERR 83886194\b' -Instruction "Enter '$ExpectedPin' in the first CredUI prompt and a different PIN in the second prompt. Both titles should be 'Pinentry for Windows'."
  ) -AllowUi)

  if ($IncludeCancelTests) {
    [void](Invoke-AssuanScenario -Name 'GETPIN cancellation returns cancelled error' -ExecutablePath $ExecutablePath -Steps @(
      New-Step 'SETDESC Cancellation prompt'
      New-Step 'GETPIN' -TerminalPattern '^ERR 83886179\b' -Instruction 'Cancel the CredUI prompt instead of entering a PIN.'
    ) -AllowUi)
  }

  [void](Invoke-AssuanScenario -Name 'CLEARPASSPHRASE removes cached passphrase' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'OPTION allow-external-password-cache'
    New-Step "CLEARPASSPHRASE $cacheKey"
    New-Step "SETKEYINFO $cacheKey"
    New-Step 'SETDESC Cache should be gone after CLEARPASSPHRASE'
    New-Step 'GETPIN' -RequiredPatterns @("(?m)^D $([Regex]::Escape($ExpectedPin) )$") -ForbiddenPatterns @('(?m)^S PASSWORD_FROM_CACHE$') -Instruction "No Windows Hello cache hit should occur. Enter '$ExpectedPin' manually."
    New-Step "CLEARPASSPHRASE $cacheKey"
  ) -AllowUi)
}

function Clear-TestVaultEntries {
  param([string] $ExecutablePath)

  Write-Step 'Removing disposable WebAuthn PRF cache'
  $cleared = -not (Test-Path -LiteralPath (Join-Path $Script:TestDataDirectory 'cache\hello.cred'))
  if (-not [string]::IsNullOrWhiteSpace($ExecutablePath)) {
    try {
      [void](Invoke-CheckedCommand -FilePath $ExecutablePath `
        -Arguments @('--clear-cache', $Script:TestDataDirectory) `
        -Description 'remove test cache credential and entries')
      $cleared = $true
    }
    catch {
      Add-Failure "Failed to remove test cache: $( $_.Exception.Message )"
      $cleared = $false
    }
  }
  elseif (-not $cleared) {
    Add-Failure 'Test cache credential exists but no executable is available to revoke it; preserving test data.'
  }

  if ($cleared -and (Test-Path -LiteralPath $Script:TestDataDirectory)) {
    try {
      Remove-Item -LiteralPath $Script:TestDataDirectory -Recurse -Force
    }
    catch {
      Add-Failure "Failed to remove test data directory: $( $_.Exception.Message )"
    }
  }
}

function Invoke-OptionalDialogTests {
  param([string] $ExecutablePath)

  if (-not $IncludeDialogTests) {
    return
  }

  Write-Step 'Manual MESSAGE and CONFIRM dialog coverage'

  [void](Invoke-AssuanScenario -Name 'MESSAGE shows command text' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'SETTITLE Message Dialog Test'
    New-Step 'MESSAGE Message dialog smoke test' -Instruction 'Click OK in the informational message dialog.'
  ) -AllowUi)

  [void](Invoke-AssuanScenario -Name 'CONFIRM --one-button shows message dialog' -ExecutablePath $ExecutablePath -Steps @(
    New-Step 'SETTITLE Confirm One Button Test'
    New-Step 'SETDESC Confirm one-button smoke test'
    New-Step 'CONFIRM --one-button' -Instruction 'Click OK in the confirmation message dialog.'
  ) -AllowUi)
}

function Invoke-LiveCacheHitTest {
  param(
    [string]    $Gpg,
    [string]    $GpgHome,
    [hashtable] $EnvVars,
    [string]    $MessagePath,
    [string]    $Fingerprint
  )

  Write-Step 'Live gpg-agent internal cache-hit test'
  Write-Host '   gpg-agent should serve the passphrase from its internal cache — no PIN prompt expected.' -ForegroundColor Yellow

  try {
    [void](Invoke-CheckedCommand `
        -FilePath $Gpg `
        -Arguments @('--homedir', $GpgHome, '--local-user', $Fingerprint, '--batch', '--yes', '--detach-sign', $MessagePath) `
        -Description 'gpg detach-sign cache-hit (no PIN prompt expected)' `
        -Environment $EnvVars)

    Write-Host '   SIGNATURE OK — confirm visually that no prompt appeared.' -ForegroundColor Green
  }
  catch {
    Add-Failure "Live cache-hit test: $( $_.Exception.Message )"
  }
}

function Invoke-LiveCacheClearTest {
  param(
    [string]    $Gpg,
    [string]    $GpgConf,
    [string]    $GpgHome,
    [hashtable] $EnvVars,
    [string]    $MessagePath,
    [string]    $Fingerprint
  )

  Write-Step 'Live gpg-agent cache-clear test'
  Write-Host "   gpg-agent will be killed to clear its internal cache." -ForegroundColor Yellow
  Write-Host "   Enter PIN '$ExpectedPin' when GPG prompts for the passphrase." -ForegroundColor Yellow

  try {
    [void](Invoke-CheckedCommand `
        -FilePath $GpgConf `
        -Arguments @('--homedir', $GpgHome, '--kill', 'gpg-agent') `
        -Description 'kill gpg-agent to clear internal passphrase cache' `
        -Environment $EnvVars)

    [void](Invoke-CheckedCommand `
        -FilePath $Gpg `
        -Arguments @('--homedir', $GpgHome, '--batch', '--yes', '--local-user', $Fingerprint, '--detach-sign', $MessagePath) `
        -Description 'gpg detach-sign after cache-clear (PIN prompt expected)' `
        -Environment $EnvVars)

    Write-Host '   PASS' -ForegroundColor Green
  }
  catch {
    Add-Failure "Live cache-clear test: $( $_.Exception.Message )"
  }
}

function Invoke-LiveDecryptTest {
  param(
    [string]    $Gpg,
    [string]    $GpgConf,
    [string]    $GpgHome,
    [hashtable] $EnvVars,
    [string]    $Fingerprint
  )

  Write-Step 'Live GPG encrypt + decrypt test'
  Write-Host "   Enter PIN '$ExpectedPin' when GPG asks to decrypt." -ForegroundColor Yellow

  $plaintextPath = Join-Path $GpgHome 'plaintext.txt'
  $encryptedPath = Join-Path $GpgHome 'encrypted.gpg'
  $decryptedPath = Join-Path $GpgHome 'decrypted.txt'
  $expectedContent = "pinentry decrypt test $Script:RunId"

  try {
    Set-Content -Path $plaintextPath -Value $expectedContent -Encoding ascii

    [void](Invoke-CheckedCommand `
        -FilePath $Gpg `
        -Arguments @('--homedir', $GpgHome, '--batch', '--yes', '--recipient', $Fingerprint,
    '--output', $encryptedPath, '--encrypt', $plaintextPath) `
        -Description 'gpg encrypt test data' `
        -Environment $EnvVars)

    [void](Invoke-CheckedCommand `
        -FilePath $GpgConf `
        -Arguments @('--homedir', $GpgHome, '--kill', 'gpg-agent') `
        -Description 'kill gpg-agent before decrypt (ensures fresh GETPIN)' `
        -Environment $EnvVars)

    [void](Invoke-CheckedCommand `
        -FilePath $Gpg `
        -Arguments @('--homedir', $GpgHome, '--batch', '--yes',
    '--output', $decryptedPath, '--decrypt', $encryptedPath) `
        -Description 'gpg decrypt test data (PIN prompt expected)' `
        -Environment $EnvVars)

    $decryptedContent = (Get-Content -Path $decryptedPath -Encoding ascii -Raw).Trim()
    Assert-True ($decryptedContent -eq $expectedContent) "Decrypted content '$decryptedContent' does not match expected '$expectedContent'."

    Write-Host '   PASS' -ForegroundColor Green
  }
  catch {
    Add-Failure "Live decrypt test: $( $_.Exception.Message )"
  }
}

function Invoke-CardStatusTest {
  param([string] $ExecutablePath, [switch] $Sign)

  Write-Step 'Read-only OpenPGP smart-card status with disposable GNUPGHOME'
  $gpgCommand = Get-Command gpg -ErrorAction SilentlyContinue
  $gpgconfCommand = Get-Command gpgconf -ErrorAction SilentlyContinue
  if ($null -eq $gpgCommand -or $null -eq $gpgconfCommand) {
    throw 'gpg and gpgconf are required for -CardStatusOnly.'
  }

  Initialize-ShortTemporaryRoot
  $tools = New-ShortGpgToolPaths -GpgPath $gpgCommand.Source -GpgConfPath $gpgconfCommand.Source
  $gpgHome = New-ShortTemporaryDirectory -Name "pfwc$Script:RunId"
  $Script:CreatedGpgHome = $gpgHome
  Set-Content -Path (Join-Path $gpgHome 'gpg-agent.conf') `
    -Value ("pinentry-program " + ($ExecutablePath -replace '\\', '/')) -Encoding ascii

  try {
    $result = Invoke-CheckedCommand -FilePath $tools.Gpg `
      -Arguments @('--homedir', $gpgHome, '--batch', '--no-tty', '--card-status') `
      -Description 'query OpenPGP card status without PIN' `
      -Environment @{ GNUPGHOME = $gpgHome; PFW_DATA_DIR = $Script:TestDataDirectory }
    Assert-True ($result.StdOut -match 'Application ID' -and $result.StdOut -match 'OpenPGP') `
      'GnuPG did not identify an OpenPGP card.'
    Write-Host '   PASS: OpenPGP card detected; serial and holder information are not displayed.' -ForegroundColor Green

    $signatureLine = $result.StdOut -split "`r?`n" | Where-Object { $_ -like 'Signature key*' } |
      Select-Object -First 1
    $fingerprint = if ($signatureLine -match ':') {
      (($signatureLine -split ':', 2)[1] -replace '\s', '').Trim()
    }
    else {
      ''
    }
    $Script:CardSigningFingerprint = if ($fingerprint -match '^(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})$') {
      $fingerprint
    }
    else {
      $null
    }
    Write-Host $( if ($Script:CardSigningFingerprint) {
      '   Card signing key: present (fingerprint withheld).'
    }
    else {
      '   Card signing key: not reported by GnuPG.'
    } ) -ForegroundColor Yellow

    $keys = Invoke-CheckedCommand -FilePath $tools.Gpg `
      -Arguments @('--homedir', $gpgHome, '--batch', '--no-tty', '--with-colons', '--list-secret-keys') `
      -Description 'check for public card-key metadata in disposable GNUPGHOME' `
      -Environment @{ GNUPGHOME = $gpgHome; PFW_DATA_DIR = $Script:TestDataDirectory }
    Write-Host $( if ($keys.StdOut -match '(?m)^sec:') {
      '   Card key metadata is available in the disposable GnuPG home.'
    }
    else {
      '   No card key metadata is available in the disposable GnuPG home.'
    } ) -ForegroundColor Yellow
  }
  catch {
    Add-Failure 'Read-only OpenPGP card status failed; no PIN or key operation was attempted.'
    return
  }

  if ($Sign) {
    try {
      Assert-True (-not [string]::IsNullOrWhiteSpace($Script:CardSigningFingerprint)) `
        'The card has no reported signing key.'
      $public = Invoke-CheckedCommand -FilePath $gpgCommand.Source `
        -Arguments @('--batch', '--armor', '--export', $Script:CardSigningFingerprint) `
        -Description 'read the matching public key from the existing GnuPG keyring'
      Assert-True ($public.StdOut -match '-----BEGIN PGP PUBLIC KEY BLOCK-----') `
        'The existing GnuPG keyring has no matching public key.'

      $publicPath = Join-Path $gpgHome 'card-public.asc'
      [System.IO.File]::WriteAllText($publicPath, $public.StdOut, $Script:Utf8NoBom)
      $envVars = @{ GNUPGHOME = $gpgHome; PFW_DATA_DIR = $Script:TestDataDirectory }
      [void](Invoke-CheckedCommand -FilePath $tools.Gpg `
          -Arguments @('--homedir', $gpgHome, '--batch', '--import', $publicPath) `
          -Description 'import only the public key into disposable GNUPGHOME' -Environment $envVars)
      [void](Invoke-CheckedCommand -FilePath $tools.Gpg `
          -Arguments @('--homedir', $gpgHome, '--batch', '--no-tty', '--card-status') `
          -Description 'associate the card with the disposable public key' -Environment $envVars)
      $keys = Invoke-CheckedCommand -FilePath $tools.Gpg `
        -Arguments @('--homedir', $gpgHome, '--batch', '--with-colons', '--list-secret-keys') `
        -Description 'check disposable card-key metadata' -Environment $envVars
      Assert-True ($keys.StdOut -match '(?m)^sec:') `
        'GnuPG could not associate a signing key on the card with its public key.'

      $message = Join-Path $gpgHome 'card-message.txt'
      $signature = Join-Path $gpgHome 'card-signature.sig'
      [System.IO.File]::WriteAllText($message, "Disposable card signing test $Script:RunId", $Script:Utf8NoBom)
      Write-Host '   ACTION: If Windows Security asks for the card PIN, enter it. Do not expect Remember.' -ForegroundColor Yellow
      [void](Invoke-CheckedCommand -FilePath $tools.Gpg `
          -Arguments @('--homedir', $gpgHome, '--batch', '--yes', '--local-user',
      $Script:CardSigningFingerprint, '--output', $signature, '--detach-sign', $message) `
          -Description 'sign disposable data with existing card key' -Environment $envVars)
      Assert-True ((Test-Path -LiteralPath $signature) -and (Get-Item -LiteralPath $signature).Length -gt 0) `
        'GnuPG did not create a detached signature.'
      Assert-True (-not (Test-Path -LiteralPath (Join-Path $Script:TestDataDirectory 'cache\hello.cred'))) `
        'A card PIN created a persistent cache credential.'
      Write-Host '   PASS: card signature created without a persistent PFW cache credential.' -ForegroundColor Green
    }
    catch {
      Add-Failure 'Card signing did not complete in disposable GNUPGHOME; no key was created or modified.'
    }
  }
}

function Invoke-OptionalGpgKeyTests {
  param([string] $ExecutablePath)

  if (-not $IncludeGpgKeyTests -and -not $GpgCacheHitOnly) {
    return
  }

  Write-Step 'Optional GPG key smoke test'

  $gpgCommand = Get-Command gpg -ErrorAction SilentlyContinue
  $gpgconfCommand = Get-Command gpgconf -ErrorAction SilentlyContinue
  if ($null -eq $gpgCommand -or $null -eq $gpgconfCommand) {
    throw 'gpg and gpgconf are required for -IncludeGpgKeyTests.'
  }

  Initialize-ShortTemporaryRoot
  $gpgTools = New-ShortGpgToolPaths -GpgPath $gpgCommand.Source -GpgConfPath $gpgconfCommand.Source
  $gpg = $gpgTools.Gpg
  $gpgconf = $gpgTools.GpgConf

  $gpgHome = New-ShortTemporaryDirectory -Name "pfwg$Script:RunId"
  New-Item -ItemType Directory -Path $gpgHome -Force | Out-Null
  $Script:CreatedGpgHome = $gpgHome

  $escapedPinentry = $ExecutablePath -replace '\\', '/'
  $cacheTtl = if ($IncludeCacheTests -or $GpgCacheHitOnly) {
    300
  }
  else {
    1
  }
  Set-Content -Path (Join-Path $gpgHome 'gpg-agent.conf') -Value @(
    "pinentry-program $escapedPinentry"
    "default-cache-ttl $cacheTtl"
    "max-cache-ttl $cacheTtl"
  ) -Encoding ascii

  $uid = "Pinentry For Windows Test $Script:RunId <pinentry-test-$Script:RunId@example.invalid>"

  Write-Host "A GPG key will be created in temporary GNUPGHOME: $gpgHome" -ForegroundColor Yellow
  Write-Host "GPG will be invoked through: $gpg" -ForegroundColor Yellow
  Write-Host "Use PIN '$ExpectedPin' when GPG asks for the passphrase." -ForegroundColor Yellow

  $envVars = @{ GNUPGHOME = $gpgHome; PFW_DATA_DIR = $Script:TestDataDirectory }
  $dirs = Invoke-CheckedCommand `
    -FilePath $gpgconf `
    -Arguments @('--homedir', $gpgHome, '--list-dirs') `
    -Description 'gpgconf list temporary directories' `
    -Environment $envVars

  $socketDirLine = ($dirs.StdOut -split "`r?`n") | Where-Object { $_ -like 'socketdir:*' } | Select-Object -First 1
  if ($socketDirLine) {
    Write-Host "GPG socket directory: $socketDirLine" -ForegroundColor Yellow
    Assert-True ($socketDirLine.Length -lt 80) "GPG socket directory is still too long after short-path setup: $socketDirLine"
  }

  [void](Invoke-CheckedCommand `
      -FilePath $gpgconf `
      -Arguments @('--homedir', $gpgHome, '--kill', 'gpg-agent') `
      -Description 'kill existing temporary gpg-agent before key generation' `
      -Environment $envVars)

  [void](Invoke-CheckedCommand `
      -FilePath $gpg `
      -Arguments @('--homedir', $gpgHome, '--batch', '--pinentry-mode', 'loopback',
  '--passphrase-fd', '0', '--quick-generate-key', $uid, 'rsa2048', 'sign', '1d') `
      -Description 'gpg temporary key generation (passphrase protected)' `
      -Environment $envVars -StandardInput $ExpectedPin)

  $list = Invoke-CheckedCommand `
    -FilePath $gpg `
    -Arguments @('--homedir', $gpgHome, '--with-colons', '--with-keygrip', '--list-secret-keys') `
    -Description 'gpg list generated secret keys' `
    -Environment $envVars

  foreach ($line in ($list.StdOut -split "`r?`n")) {
    if ( $line.StartsWith('fpr:')) {
      $parts = $line.Split(':')
      if ($parts.Length -gt 9 -and -not [string]::IsNullOrWhiteSpace($parts[9])) {
        [void] $Script:CreatedGpgFingerprints.Add($parts[9])
      }
    }
  }

  Assert-True ($Script:CreatedGpgFingerprints.Count -gt 0) 'GPG key was generated but no fingerprint was found for cleanup.'

  if ($IncludeDecryptTest) {
    [void](Invoke-CheckedCommand `
        -FilePath $gpg `
        -Arguments @('--homedir', $gpgHome, '--batch', '--pinentry-mode', 'loopback',
    '--passphrase-fd', '0', '--quick-add-key',
    $Script:CreatedGpgFingerprints[0], 'rsa2048', 'encr', '1d') `
        -Description 'gpg add encryption subkey for decrypt test' `
        -Environment $envVars -StandardInput $ExpectedPin)
  }

  $messagePath = Join-Path $gpgHome 'message.txt'
  Set-Content -Path $messagePath -Value "pinentry smoke test $Script:RunId" -Encoding ascii

  [void](Invoke-CheckedCommand `
      -FilePath $gpgconf `
      -Arguments @('--homedir', $gpgHome, '--kill', 'gpg-agent') `
      -Description 'clear key-generation loopback passphrase before live prompt' `
      -Environment $envVars)

  if ($ProvisionGpgOnly) {
    Write-Host '   Disposable GPG key provisioned; cleanup follows without an interactive prompt.' -ForegroundColor Green
    return
  }

  Write-Host "   ACTION: Enter '$ExpectedPin' when the credential prompt appears. Do not check Remember." -ForegroundColor Yellow
  [void](Invoke-CheckedCommand `
      -FilePath $gpg `
      -Arguments @('--homedir', $gpgHome, '--batch', '--yes', '--local-user', $Script:CreatedGpgFingerprints[0], '--detach-sign', $messagePath) `
      -Description 'gpg detach-sign smoke test (PIN prompt expected)' `
      -Environment $envVars)

  if ($IncludeCacheTests -or $GpgCacheHitOnly) {
    if ($GpgCacheHitOnly) {
      Write-Host '   First signature completed. The repeat begins in 5 seconds; watch for ANY new window.' -ForegroundColor Yellow
      Start-Sleep -Seconds 5
    }
    Invoke-LiveCacheHitTest `
      -Gpg $gpg -GpgHome $gpgHome -EnvVars $envVars `
      -MessagePath $messagePath -Fingerprint $Script:CreatedGpgFingerprints[0]
  }

  if ($IncludeCacheTests) {
    Invoke-LiveCacheClearTest `
      -Gpg $gpg -GpgConf $gpgconf -GpgHome $gpgHome -EnvVars $envVars `
      -MessagePath $messagePath -Fingerprint $Script:CreatedGpgFingerprints[0]
  }

  if ($IncludeDecryptTest) {
    Invoke-LiveDecryptTest `
      -Gpg $gpg -GpgConf $gpgconf -GpgHome $gpgHome -EnvVars $envVars `
      -Fingerprint $Script:CreatedGpgFingerprints[0]
  }
}

function New-ShortGpgToolPaths {
  param(
    [string] $GpgPath,
    [string] $GpgConfPath
  )

  $gpgBin = Split-Path $GpgPath -Parent
  $gpgRoot = Split-Path $gpgBin -Parent
  $linkRoot = New-ShortTemporaryDirectory -Name "pfwgr$Script:RunId"
  Remove-Item -LiteralPath $linkRoot -Force

  try {
    New-Item -ItemType Junction -Path $linkRoot -Target $gpgRoot -Force | Out-Null
    $Script:CreatedGpgRootLink = $linkRoot

    return [pscustomobject]@{
      Gpg = Join-Path $linkRoot 'bin\gpg.exe'
      GpgConf = Join-Path $linkRoot 'bin\gpgconf.exe'
    }
  }
  catch {
    Write-Host "   WARN: failed to create short GnuPG junction '$linkRoot': $( $_.Exception.Message )" -ForegroundColor Yellow
    Write-Host '   WARN: falling back to PATH GnuPG; Scoop portable installs may fail with socket path length errors.' -ForegroundColor Yellow

    return [pscustomobject]@{
      Gpg = $GpgPath
      GpgConf = $GpgConfPath
    }
  }
}

function Initialize-ShortTemporaryRoot {
  if (-not [string]::IsNullOrWhiteSpace($Script:ShortTempRoot)) {
    return
  }

  $backingDir = Join-Path $Script:RepoRoot "artifacts\pfwsubst$Script:RunId"
  New-Item -ItemType Directory -Path $backingDir -Force | Out-Null
  $Script:CreatedSubstBackingDir = $backingDir

  foreach ($letter in 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z') {
    $drive = "${letter}:"
    if (Test-Path $drive) {
      continue
    }

    $substResult = & subst.exe $drive $backingDir 2>&1
    if ($LASTEXITCODE -eq 0 -and (Test-Path $drive)) {
      $Script:CreatedSubstDrive = $drive
      $Script:ShortTempRoot = "$drive\"
      return
    }

    if ($substResult) {
      Write-Host "   WARN: subst $drive failed: $substResult" -ForegroundColor Yellow
    }
  }

  Write-Host '   WARN: no temporary subst drive was available; falling back to repository artifacts.' -ForegroundColor Yellow
  $Script:ShortTempRoot = Join-Path $Script:RepoRoot 'artifacts'
}

function New-ShortTemporaryDirectory {
  param([string] $Name)

  $roots = @($Script:ShortTempRoot) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -Unique

  foreach ($root in $roots) {
    try {
      if (-not (Test-Path $root)) {
        New-Item -ItemType Directory -Path $root -Force | Out-Null
      }

      $path = Join-Path $root $Name
      New-Item -ItemType Directory -Path $path -Force | Out-Null
      return (Resolve-Path $path).Path
    }
    catch {
    }
  }

  throw 'Unable to create a short temporary directory for the GPG smoke test.'
}

function Remove-OptionalGpgKeys {
  if (-not [string]::IsNullOrWhiteSpace($Script:CreatedGpgHome)) {
    Write-Step 'Cleaning temporary GPG keys and home'

    $gpgCommand = Get-Command gpg -ErrorAction SilentlyContinue
    $gpgconfCommand = Get-Command gpgconf -ErrorAction SilentlyContinue
    $gpg = if ($null -ne $gpgCommand) {
      $gpgCommand.Source
    }
    else {
      $null
    }
    $gpgconf = if ($null -ne $gpgconfCommand) {
      $gpgconfCommand.Source
    }
    else {
      $null
    }

    if (-not [string]::IsNullOrWhiteSpace($Script:CreatedGpgRootLink)) {
      $linkedGpg = Join-Path $Script:CreatedGpgRootLink 'bin\gpg.exe'
      $linkedGpgConf = Join-Path $Script:CreatedGpgRootLink 'bin\gpgconf.exe'

      if (Test-Path $linkedGpg) {
        $gpg = $linkedGpg
      }

      if (Test-Path $linkedGpgConf) {
        $gpgconf = $linkedGpgConf
      }
    }

    $envVars = @{ GNUPGHOME = $Script:CreatedGpgHome }

    if (-not [string]::IsNullOrWhiteSpace($gpg)) {
      foreach ($fingerprint in ($Script:CreatedGpgFingerprints | Select-Object -Unique)) {
        try {
          [void](Invoke-CheckedCommand `
            -FilePath $gpg `
            -Arguments @('--homedir', $Script:CreatedGpgHome, '--batch', '--yes', '--delete-secret-and-public-key', $fingerprint) `
            -Description "delete temporary GPG key $fingerprint" `
            -Environment $envVars)
        }
        catch {
          Add-Failure "Failed to delete temporary GPG key ${fingerprint}: $( $_.Exception.Message )"
        }
      }
    }

    if (-not [string]::IsNullOrWhiteSpace($gpgconf)) {
      try {
        [void](Invoke-CheckedCommand `
          -FilePath $gpgconf `
          -Arguments @('--homedir', $Script:CreatedGpgHome, '--kill', 'gpg-agent') `
          -Description 'kill temporary gpg-agent' `
          -Environment $envVars)
      }
      catch {
        Write-Host "   WARN: failed to kill temporary gpg-agent: $( $_.Exception.Message )" -ForegroundColor Yellow
      }
    }

    try {
      Remove-Item -LiteralPath $Script:CreatedGpgHome -Recurse -Force
    }
    catch {
      Add-Failure "Failed to remove temporary GNUPGHOME '$Script:CreatedGpgHome': $( $_.Exception.Message )"
    }
  }

  if (-not [string]::IsNullOrWhiteSpace($Script:CreatedGpgRootLink) -and
    (Test-Path $Script:CreatedGpgRootLink)) {
    try {
      Remove-Item -LiteralPath $Script:CreatedGpgRootLink -Force
    }
    catch {
      Add-Failure "Failed to remove temporary GnuPG junction '$Script:CreatedGpgRootLink': $( $_.Exception.Message )"
    }
  }

  if (-not [string]::IsNullOrWhiteSpace($Script:CreatedSubstDrive)) {
    try {
      & subst.exe $Script:CreatedSubstDrive /D | Out-Null
      if ($LASTEXITCODE -ne 0) {
        throw "subst.exe returned exit code $LASTEXITCODE."
      }
    }
    catch {
      Add-Failure "Failed to remove temporary subst drive '$Script:CreatedSubstDrive': $( $_.Exception.Message )"
    }
  }

  if (-not [string]::IsNullOrWhiteSpace($Script:CreatedSubstBackingDir) -and
    (Test-Path $Script:CreatedSubstBackingDir)) {
    try {
      if (@(Get-ChildItem -LiteralPath $Script:CreatedSubstBackingDir -Force).Count -eq 0) {
        Remove-Item -LiteralPath $Script:CreatedSubstBackingDir -Force
      }
      else {
        Add-Warning 'Temporary subst backing directory was not empty and was preserved for review.'
      }
    }
    catch {
      Add-Failure "Failed to remove temporary subst backing directory '$Script:CreatedSubstBackingDir': $( $_.Exception.Message )"
    }
  }
}

try {
  if (($CardStatusOnly -or $CardSignOnly) -and ($Interactive -or $RequireWindowsHelloCache -or $IncludeDialogTests -or
    $IncludeCancelTests -or $IncludeGpgKeyTests -or $ProvisionGpgOnly -or $IncludeDecryptTest -or
    $IncludeCacheTests -or $GpgCacheHitOnly -or ($CardStatusOnly -and $CardSignOnly))) {
    throw 'Card status/signing mode must run alone (apart from build and timeout options).'
  }

  if ($GpgCacheHitOnly -and ($Interactive -or $RequireWindowsHelloCache -or
    $IncludeDialogTests -or $IncludeCancelTests -or $IncludeGpgKeyTests -or
    $ProvisionGpgOnly -or $IncludeDecryptTest -or $IncludeCacheTests)) {
    throw '-GpgCacheHitOnly must run alone (apart from build and timeout options).'
  }
  if ($ProvisionGpgOnly -and -not $IncludeGpgKeyTests) {
    throw '-ProvisionGpgOnly requires -IncludeGpgKeyTests.'
  }
  if (($IncludeCacheTests -or $IncludeDecryptTest) -and -not $IncludeGpgKeyTests) {
    throw '-IncludeCacheTests and -IncludeDecryptTest require -IncludeGpgKeyTests.'
  }
  if ($ProvisionGpgOnly -and ($IncludeCacheTests -or $IncludeDecryptTest)) {
    throw '-ProvisionGpgOnly cannot be combined with live cache or decrypt scenarios.'
  }
  if (($RequireWindowsHelloCache -or $IncludeCancelTests) -and -not $Interactive) {
    throw '-RequireWindowsHelloCache and -IncludeCancelTests require -Interactive.'
  }

  New-Item -ItemType Directory -Path $Script:TestDataDirectory -Force | Out-Null
  Build-Pinentry
  $pinentryExe = Get-PinentryExecutable
  Write-Step "Using $pinentryExe"

  if ($CardStatusOnly -or $CardSignOnly) {
    Invoke-CardStatusTest -ExecutablePath $pinentryExe -Sign:$CardSignOnly
  }
  elseif (-not $GpgCacheHitOnly) {
    Invoke-ProtocolAndOptionTests -ExecutablePath $pinentryExe

    if ($Interactive) {
      Invoke-InteractivePinentryTests -ExecutablePath $pinentryExe
    }
    else {
      Write-Step 'Skipping interactive GETPIN/CredUI/Windows Hello scenarios'
      Write-Host 'Run again with -Interactive to cover prompts, cache hits, Windows Hello, repeat success, and repeat mismatch.' -ForegroundColor Yellow
    }

    Invoke-OptionalDialogTests -ExecutablePath $pinentryExe
  }

  Invoke-OptionalGpgKeyTests -ExecutablePath $pinentryExe
}
finally {
  try {
    Clear-TestVaultEntries -ExecutablePath $( if (Test-Path variable:pinentryExe) {
      $pinentryExe
    }
    else {
      $null
    } )
  }
  finally {
    Remove-OptionalGpgKeys
  }
}

if ($Script:Warnings.Count -gt 0) {
  Write-Host ""
  Write-Host 'Test warnings:' -ForegroundColor Yellow
  foreach ($warning in $Script:Warnings) {
    Write-Host " - $warning" -ForegroundColor Yellow
  }
}

if ($Script:Failures.Count -gt 0) {
  Write-Host ""
  Write-Host 'Test failures:' -ForegroundColor Red
  foreach ($failure in $Script:Failures) {
    Write-Host " - $failure" -ForegroundColor Red
  }

  exit 1
}

Write-Host ""
if ($Script:Warnings.Count -gt 0) {
  Write-Host 'All requested PinentryForWindows tests completed successfully with warnings.' -ForegroundColor Green
}
else {
  Write-Host 'All requested PinentryForWindows tests completed successfully.' -ForegroundColor Green
}
