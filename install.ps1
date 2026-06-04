$ErrorActionPreference = 'Stop'

$Repository = 'baliestri/pinentry-for-windows'

function Write-Step {
  param([Parameter(Mandatory = $true)][string]$Message)
  Write-Host "[pinentry-for-windows] $Message"
}

function Enable-Tls12 {
  Write-Step 'Enabling TLS 1.2 for GitHub requests.'

  try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
  }
  catch {
    Write-Step 'Could not explicitly enable TLS 1.2; continuing with the current PowerShell defaults.'
  }
}

function Get-WindowsRuntimeIdentifierCandidates {
  Write-Step 'Detecting Windows architecture.'

  $architecture = if (-not [string]::IsNullOrWhiteSpace($env:PROCESSOR_ARCHITEW6432)) {
    $env:PROCESSOR_ARCHITEW6432
  }
  else {
    $env:PROCESSOR_ARCHITECTURE
  }

  switch ($architecture.ToUpperInvariant()) {
    'AMD64' { return @('win-x64') }
    'X86' { return @('win-x86') }
    'ARM64' { return @('win-arm64', 'win-x64') }
    default {
      throw "Unsupported architecture: $architecture."
    }
  }
}

function Convert-GpgPath {
  param([Parameter(Mandatory = $true)][string]$Path)

  $normalizedPath = $Path.Trim()

  if ($normalizedPath -match '^([A-Za-z])%3[aA][\\/](.*)$') {
    $normalizedPath = "$($Matches[1]):\$($Matches[2])"
  }
  elseif ($normalizedPath -match '^/([A-Za-z])/(.*)$') {
    $normalizedPath = "$($Matches[1]):\$($Matches[2])"
  }

  $normalizedPath = $normalizedPath -replace '/', '\'
  return [System.Uri]::UnescapeDataString($normalizedPath)
}

function Invoke-GpgConfListDir {
  param(
    [Parameter(Mandatory = $true)][string]$GpgConfPath,
    [Parameter(Mandatory = $true)][string]$Name
  )

  Write-Step "Reading GnuPG $Name with gpgconf."
  $value = & $GpgConfPath --list-dirs $Name

  if ($LASTEXITCODE -ne 0) {
    throw "gpgconf failed while reading $Name."
  }

  if ([string]::IsNullOrWhiteSpace($value)) {
    throw "gpgconf returned an empty value for $Name."
  }

  return Convert-GpgPath $value
}

function Get-GpgConfPath {
  Write-Step 'Looking for gpgconf in PATH.'
  $command = Get-Command gpgconf -ErrorAction SilentlyContinue

  if ($null -eq $command) {
    throw 'gpgconf was not found in PATH. Install GnuPG and make sure gpgconf is available.'
  }

  Write-Step "Using gpgconf at $($command.Source)."
  return $command.Source
}

function Get-LatestRelease {
  Write-Step 'Querying the latest GitHub release.'
  $releaseUri = "https://api.github.com/repos/$Repository/releases/latest"
  return Invoke-RestMethod -Uri $releaseUri -Headers @{ 'User-Agent' = 'pinentry-for-windows-installer' }
}

function Get-ReleaseAsset {
  param(
    [Parameter(Mandatory = $true)]$Release,
    [Parameter(Mandatory = $true)][string[]]$RuntimeIdentifiers
  )

  foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    $assetName = "pinentry-for-windows.$runtimeIdentifier.exe"
    Write-Step "Looking for release asset $assetName."

    $asset = $Release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1

    if ($null -ne $asset) {
      return $asset
    }
  }

  throw "Release $($Release.tag_name) does not contain a compatible Windows asset."
}

function Download-Asset {
  param(
    [Parameter(Mandatory = $true)]$Asset,
    [Parameter(Mandatory = $true)][string]$DestinationPath
  )

  Write-Step "Downloading $($Asset.name) to $DestinationPath."
  $webClient = New-Object System.Net.WebClient
  $webClient.Headers.Add('User-Agent', 'pinentry-for-windows-installer')

  try {
    $webClient.DownloadFile($Asset.browser_download_url, $DestinationPath)
  }
  finally {
    $webClient.Dispose()
  }
}

function Convert-ToGpgConfigPath {
  param([Parameter(Mandatory = $true)][string]$Path)
  return ($Path -replace '\\', '/')
}

function Confirm-ReplacePinentryProgram {
  param([Parameter(Mandatory = $true)][string[]]$ExistingLines)

  Write-Step 'Existing gpg-agent.conf pinentry-program setting found.'
  foreach ($existingLine in $ExistingLines) {
    Write-Step "Existing entry: $existingLine"
  }

  $answer = Read-Host 'Replace the existing pinentry-program setting? [y/N]'
  return $answer -match '^(y|yes)$'
}

function Write-TextFileNoBom {
  param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string[]]$Lines
  )

  $encoding = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllLines($Path, $Lines, $encoding)
}

function Set-PinentryProgramConfig {
  param(
    [Parameter(Mandatory = $true)][string]$ConfigPath,
    [Parameter(Mandatory = $true)][string]$PinentryPath
  )

  Write-Step "Configuring gpg-agent.conf at $ConfigPath."

  $configDirectory = Split-Path -Parent $ConfigPath
  if (-not (Test-Path -LiteralPath $configDirectory)) {
    Write-Step "Creating GnuPG config directory $configDirectory."
    New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
  }

  if (-not (Test-Path -LiteralPath $ConfigPath)) {
    Write-Step 'Creating gpg-agent.conf.'
    New-Item -ItemType File -Path $ConfigPath -Force | Out-Null
  }

  $lines = @(Get-Content -LiteralPath $ConfigPath -ErrorAction SilentlyContinue)
  $existingLines = @($lines | Where-Object { $_ -match '^\s*pinentry-program\s+' })
  $newLine = "pinentry-program $(Convert-ToGpgConfigPath $PinentryPath)"

  if ($existingLines.Count -gt 0) {
    if (-not (Confirm-ReplacePinentryProgram $existingLines)) {
      Write-Step 'Leaving existing pinentry-program setting unchanged.'
      return $false
    }

    Write-Step 'Replacing existing pinentry-program setting.'
    $lines = @($lines | Where-Object { $_ -notmatch '^\s*pinentry-program\s+' })
    $lines += $newLine
  }
  else {
    Write-Step 'Adding pinentry-program setting.'
    $lines += $newLine
  }

  Write-TextFileNoBom -Path $ConfigPath -Lines $lines
  return $true
}

function Restart-GpgAgent {
  param([Parameter(Mandatory = $true)][string]$GpgConfPath)

  Write-Step 'Restarting gpg-agent so it reads the updated configuration.'
  & $GpgConfPath --kill gpg-agent

  if ($LASTEXITCODE -ne 0) {
    Write-Step 'gpgconf could not kill gpg-agent. Restart it manually if the new pinentry is not used immediately.'
  }
}

try {
  if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') {
    throw 'This installer must be run on Windows.'
  }

  Enable-Tls12

  $runtimeIdentifiers = Get-WindowsRuntimeIdentifierCandidates
  Write-Step "Compatible release asset types: $($runtimeIdentifiers -join ', ')."

  $gpgConfPath = Get-GpgConfPath
  $binDirectory = Invoke-GpgConfListDir -GpgConfPath $gpgConfPath -Name 'bindir'
  $homeDirectory = Invoke-GpgConfListDir -GpgConfPath $gpgConfPath -Name 'homedir'

  Write-Step "Using GnuPG bin directory $binDirectory."
  Write-Step "Using GnuPG home directory $homeDirectory."

  if (-not (Test-Path -LiteralPath $binDirectory)) {
    throw "GnuPG bin directory does not exist: $binDirectory."
  }

  $release = Get-LatestRelease
  Write-Step "Latest release is $($release.tag_name)."

  $asset = Get-ReleaseAsset -Release $release -RuntimeIdentifiers $runtimeIdentifiers
  Write-Step "Selected release asset $($asset.name)."
  $temporaryPath = Join-Path ([System.IO.Path]::GetTempPath()) $asset.name
  Download-Asset -Asset $asset -DestinationPath $temporaryPath

  $installPath = Join-Path $binDirectory $asset.name
  Write-Step "Installing executable to $installPath."
  Move-Item -LiteralPath $temporaryPath -Destination $installPath -Force

  $agentConfigPath = Join-Path $homeDirectory 'gpg-agent.conf'
  $configChanged = Set-PinentryProgramConfig -ConfigPath $agentConfigPath -PinentryPath $installPath

  if ($configChanged) {
    Restart-GpgAgent -GpgConfPath $gpgConfPath
  }

  Write-Step 'Installation complete.'
}
catch {
  Write-Error $_.Exception.Message
  exit 1
}
