param(
  [switch]$Update,
  [switch]$Uninstall,
  [switch]$NoRestartAgent,
  [string]$InstallDirectory,
  [string]$DataDirectory,
  [string]$BrokerDirectory
)

$ErrorActionPreference = 'Stop'

$Repository = 'baliestri/pinentry-for-windows'
$PinentryProgramPattern = '^\s*pinentry-program\s+'
$PreviousPinentryProgramPrefix = '# pinentry-for-windows previous-pinentry-program: '
$PinentryExecutablePattern = 'pfw.*.exe'
$PinentryExecutableRegex = '^pfw\.(x64|arm64)\.exe$'
$ChecksumAssetName = 'SHA256SUMS.txt'
$BrokerRunPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$BrokerRunName = 'PinentryForWindowsBroker'

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

  switch ( $architecture.ToUpperInvariant()) {
    'AMD64' {
      return @('x64')
    }
    'ARM64' {
      return @('arm64')
    }
    default {
      throw "Unsupported architecture: $architecture."
    }
  }
}

function Convert-GpgPath {
  param([Parameter(Mandatory = $true)][string]$Path)

  $normalizedPath = $Path.Trim()

  if ($normalizedPath -match '^([A-Za-z])%3[aA][\\/](.*)$') {
    $normalizedPath = "$( $Matches[1] ):\$( $Matches[2] )"
  }
  elseif ($normalizedPath -match '^/([A-Za-z])/(.*)$') {
    $normalizedPath = "$( $Matches[1] ):\$( $Matches[2] )"
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

  if ( [string]::IsNullOrWhiteSpace($value)) {
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

  Write-Step "Using gpgconf at $( $command.Source )."
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
    $assetName = "pfw.$runtimeIdentifier.exe"
    $asset = Get-ReleaseAssetByName -Release $Release -AssetName $assetName

    if ($null -ne $asset) {
      return $asset
    }
  }

  throw "Release $( $Release.tag_name ) does not contain a compatible Windows asset."
}

function Get-ReleaseAssetByName {
  param(
    [Parameter(Mandatory = $true)]$Release,
    [Parameter(Mandatory = $true)][string]$AssetName
  )

  Write-Step "Looking for release asset $AssetName."
  return $Release.assets | Where-Object { $_.name -eq $AssetName } | Select-Object -First 1
}

function Get-ReleaseChecksumAsset {
  param([Parameter(Mandatory = $true)]$Release)

  $asset = Get-ReleaseAssetByName -Release $Release -AssetName $ChecksumAssetName

  if ($null -eq $asset) {
    throw "Release $( $Release.tag_name ) does not contain $ChecksumAssetName."
  }

  return $asset
}

function Download-Asset {
  param(
    [Parameter(Mandatory = $true)]$Asset,
    [Parameter(Mandatory = $true)][string]$DestinationPath
  )

  Write-Step "Downloading $( $Asset.name ) to $DestinationPath."
  $webClient = New-Object System.Net.WebClient
  $webClient.Headers.Add('User-Agent', 'pinentry-for-windows-installer')

  try {
    $webClient.DownloadFile($Asset.browser_download_url, $DestinationPath)
  }
  finally {
    $webClient.Dispose()
  }
}

function Get-ExpectedAssetChecksum {
  param(
    [Parameter(Mandatory = $true)][string]$ChecksumPath,
    [Parameter(Mandatory = $true)][string]$AssetName
  )

  $checksumLinePattern = '^\s*(?<Hash>[A-Fa-f0-9]{64})\s+\*?(?<Name>.+?)\s*$'
  $assetNamePattern = '\s+\*?' + [regex]::Escape($AssetName) + '\s*$'

  foreach ($line in (Get-Content -LiteralPath $ChecksumPath)) {
    if ( [string]::IsNullOrWhiteSpace($line)) {
      continue
    }

    if ($line -match $checksumLinePattern) {
      if ($Matches['Name'].Trim() -eq $AssetName) {
        return $Matches['Hash']
      }

      continue
    }

    if ($line -match $assetNamePattern) {
      throw "Checksum entry for $AssetName in $ChecksumAssetName is malformed."
    }
  }

  throw "$ChecksumAssetName does not contain a checksum entry for $AssetName."
}

function Assert-DownloadedAssetChecksum {
  param(
    [Parameter(Mandatory = $true)][string]$AssetName,
    [Parameter(Mandatory = $true)][string]$AssetPath,
    [Parameter(Mandatory = $true)][string]$ChecksumPath
  )

  Write-Step "Verifying SHA-256 checksum for $AssetName."
  $expectedHash = Get-ExpectedAssetChecksum -ChecksumPath $ChecksumPath -AssetName $AssetName
  $actualHash = (Get-FileHash -LiteralPath $AssetPath -Algorithm SHA256).Hash

  if (-not [string]::Equals($expectedHash, $actualHash, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Checksum verification failed for $AssetName. Expected $expectedHash but computed $actualHash."
  }

  Write-Step "Checksum verification succeeded for $AssetName."
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

function Confirm-RestorePreviousPinentryProgram {
  param([Parameter(Mandatory = $true)][string]$PreviousLine)

  Write-Step 'Pinentry For Windows is currently configured in gpg-agent.conf.'
  Write-Step "Previous entry available for restore: $PreviousLine"
  $answer = Read-Host 'Restore previous setting, delete setting, or cancel uninstall? [r/d/C]'

  if ( [string]::IsNullOrWhiteSpace($answer)) {
    return 'Cancel'
  }

  switch -Regex ( $answer.Trim()) {
    '^(r|restore)$' {
      return 'Restore'
    }
    '^(d|delete|remove)$' {
      return 'Delete'
    }
    default {
      return 'Cancel'
    }
  }
}

function Confirm-RemovePinentryProgram {
  Write-Step 'Pinentry For Windows is currently configured in gpg-agent.conf.'
  Write-Step 'No previous pinentry-program setting was recorded by the installer.'
  $answer = Read-Host 'Remove the Pinentry For Windows pinentry-program setting? [y/N]'
  return $answer -match '^(y|yes)$'
}

function Write-TextFileNoBom {
  param(
    [Parameter(Mandatory = $true)][string]$Path,
    [AllowEmptyCollection()][string[]]$Lines = @()
  )

  $encoding = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllLines($Path, $Lines, $encoding)
}

function Backup-ConfigFile {
  param([Parameter(Mandatory = $true)][string]$ConfigPath)

  if (-not (Test-Path -LiteralPath $ConfigPath)) {
    return
  }

  $timestamp = Get-Date -Format 'yyyyMMddHHmmssfff'
  $backupPath = "$ConfigPath.pinentry-for-windows.$timestamp.$([Guid]::NewGuid().ToString('N') ).bak"
  Write-Step "Backing up gpg-agent.conf to $backupPath."
  Copy-Item -LiteralPath $ConfigPath -Destination $backupPath -Force
}

function Get-PinentryProgramLines {
  param([AllowEmptyCollection()][string[]]$Lines = @())
  return @($Lines | Where-Object { $_ -match $PinentryProgramPattern })
}

function Get-PinentryProgramPathFromLine {
  param([Parameter(Mandatory = $true)][string]$Line)

  if ($Line -notmatch $PinentryProgramPattern) {
    return $null
  }

  $path = ($Line -replace $PinentryProgramPattern, '').Trim()

  if (($path.StartsWith('"') -and $path.EndsWith('"')) -or ($path.StartsWith("'") -and $path.EndsWith("'"))) {
    $path = $path.Substring(1, $path.Length - 2)
  }

  return Convert-GpgPath $path
}

function Test-PinentryPathIsPinentryForWindows {
  param([string]$Path)

  if ( [string]::IsNullOrWhiteSpace($Path)) {
    return $false
  }

  return (Split-Path -Leaf $Path) -match $PinentryExecutableRegex
}

function Test-PinentryProgramLineIsPinentryForWindows {
  param([Parameter(Mandatory = $true)][string]$Line)
  return Test-PinentryPathIsPinentryForWindows (Get-PinentryProgramPathFromLine $Line)
}

function Test-PinentryProgramLineInInstallDirectory {
  param(
    [Parameter(Mandatory = $true)][string]$Line,
    [Parameter(Mandatory = $true)][string]$InstallDirectory
  )

  $path = Get-PinentryProgramPathFromLine $Line
  if (-not (Test-PinentryPathIsPinentryForWindows $path)) {
    return $false
  }

  $directory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($path))
  $installed = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($InstallDirectory))
  return [string]::Equals($directory, $installed, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-PreviousPinentryProgramLine {
  param([AllowEmptyCollection()][string[]]$Lines = @())

  foreach ($line in $Lines) {
    if (-not $line.StartsWith($PreviousPinentryProgramPrefix)) {
      continue
    }

    $previousLine = $line.Substring($PreviousPinentryProgramPrefix.Length).Trim()
    if ($previousLine -match $PinentryProgramPattern) {
      return $previousLine
    }
  }

  return $null
}

function Remove-PinentryProgramAndMetadataLines {
  param([AllowEmptyCollection()][string[]]$Lines = @())

  return @($Lines | Where-Object {
      $_ -notmatch $PinentryProgramPattern -and -not $_.StartsWith($PreviousPinentryProgramPrefix)
    })
}

function Test-LineArraysEqual {
  param(
    [AllowEmptyCollection()][string[]]$Left = @(),
    [AllowEmptyCollection()][string[]]$Right = @()
  )

  if ($Left.Count -ne $Right.Count) {
    return $false
  }

  for ($index = 0; $index -lt $Left.Count; $index++) {
    if ($Left[$index] -ne $Right[$index]) {
      return $false
    }
  }

  return $true
}

function Resolve-InstallDirectory {
  param(
    [string]$RequestedInstallDirectory,
    [Parameter(Mandatory = $true)][string]$DefaultInstallDirectory
  )

  if ( [string]::IsNullOrWhiteSpace($RequestedInstallDirectory)) {
    return $DefaultInstallDirectory
  }

  $expandedPath = [Environment]::ExpandEnvironmentVariables($RequestedInstallDirectory)
  return [System.IO.Path]::GetFullPath($expandedPath)
}

function Ensure-InstallDirectory {
  param([Parameter(Mandatory = $true)][string]$Directory)

  if (-not (Test-Path -LiteralPath $Directory)) {
    Write-Step "Creating install directory $Directory."
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
  }
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
  $existingLines = @(Get-PinentryProgramLines $lines)
  $newLine = "pinentry-program $( Convert-ToGpgConfigPath $PinentryPath )"
  $updatedLines = @($lines)

  if ($existingLines.Count -gt 0) {
    $allExistingLinesArePinentryForWindows = $true
    foreach ($existingLine in $existingLines) {
      if (-not (Test-PinentryProgramLineInInstallDirectory -Line $existingLine -InstallDirectory (Split-Path -Parent $PinentryPath))) {
        $allExistingLinesArePinentryForWindows = $false
        break
      }
    }

    $updatedLines = @(Remove-PinentryProgramAndMetadataLines $lines)
    $previousLine = Get-PreviousPinentryProgramLine $lines

    if ($allExistingLinesArePinentryForWindows) {
      Write-Step 'Updating existing Pinentry For Windows pinentry-program setting.'

      if (-not [string]::IsNullOrWhiteSpace($previousLine)) {
        $updatedLines += "$PreviousPinentryProgramPrefix$previousLine"
      }

      $updatedLines += $newLine
    }
    else {
      if (-not (Confirm-ReplacePinentryProgram $existingLines)) {
        Write-Step 'Leaving existing pinentry-program setting unchanged.'
        return $false
      }

      Write-Step 'Replacing existing pinentry-program setting.'
      $previousCandidateLine = $existingLines | Where-Object {
        -not (Test-PinentryProgramLineInInstallDirectory -Line $_ -InstallDirectory (Split-Path -Parent $PinentryPath))
      } | Select-Object -First 1
      $updatedLines += "$PreviousPinentryProgramPrefix$($previousCandidateLine.Trim() )"
      $updatedLines += $newLine
    }
  }
  else {
    Write-Step 'Adding pinentry-program setting.'
    $updatedLines += $newLine
  }

  if (Test-LineArraysEqual -Left $lines -Right $updatedLines) {
    Write-Step 'gpg-agent.conf already contains the requested pinentry-program setting.'
    return $false
  }

  Backup-ConfigFile -ConfigPath $ConfigPath
  Write-TextFileNoBom -Path $ConfigPath -Lines $updatedLines
  return $true
}

function Remove-PinentryProgramConfig {
  param(
    [Parameter(Mandatory = $true)][string]$ConfigPath,
    [Parameter(Mandatory = $true)][string]$InstallDirectory
  )

  if (-not (Test-Path -LiteralPath $ConfigPath)) {
    Write-Step 'gpg-agent.conf does not exist; no pinentry-program setting to remove.'
    return $false
  }

  Write-Step "Checking gpg-agent.conf at $ConfigPath."
  $lines = @(Get-Content -LiteralPath $ConfigPath -ErrorAction SilentlyContinue)
  $existingLines = @(Get-PinentryProgramLines $lines)

  if ($existingLines.Count -eq 0) {
    Write-Step 'No pinentry-program setting found in gpg-agent.conf.'
    return $false
  }

  $hasPinentryForWindowsLine = $false
  $hasOtherPinentryProgramLine = $false
  foreach ($existingLine in $existingLines) {
    if (Test-PinentryProgramLineInInstallDirectory -Line $existingLine -InstallDirectory $InstallDirectory) {
      $hasPinentryForWindowsLine = $true
    }
    else {
      $hasOtherPinentryProgramLine = $true
    }
  }

  if (-not $hasPinentryForWindowsLine) {
    Write-Step 'The current pinentry-program setting does not point to Pinentry For Windows; leaving it unchanged.'
    return $false
  }

  if ($hasOtherPinentryProgramLine) {
    throw 'Both installed and unrelated pinentry-program settings were found. Review gpg-agent.conf before uninstalling.'
  }

  $updatedLines = @(Remove-PinentryProgramAndMetadataLines $lines)
  $previousLine = Get-PreviousPinentryProgramLine $lines

  if (-not [string]::IsNullOrWhiteSpace($previousLine)) {
    $action = Confirm-RestorePreviousPinentryProgram -PreviousLine $previousLine

    if ($action -eq 'Restore') {
      Write-Step 'Restoring previous pinentry-program setting.'
      $updatedLines += $previousLine
    }
    elseif ($action -eq 'Delete') {
      Write-Step 'Removing Pinentry For Windows pinentry-program setting.'
    }
    else {
      throw 'Uninstall cancelled before changing gpg-agent.conf.'
    }
  }
  else {
    if (-not (Confirm-RemovePinentryProgram)) {
      throw 'Uninstall cancelled before changing gpg-agent.conf.'
    }

    Write-Step 'Removing Pinentry For Windows pinentry-program setting.'
  }

  if (Test-LineArraysEqual -Left $lines -Right $updatedLines) {
    Write-Step 'gpg-agent.conf does not need changes.'
    return $false
  }

  Backup-ConfigFile -ConfigPath $ConfigPath
  Write-TextFileNoBom -Path $ConfigPath -Lines $updatedLines
  return $true
}

function Get-ConfiguredPinentryForWindowsPaths {
  param([Parameter(Mandatory = $true)][string]$ConfigPath)

  if (-not (Test-Path -LiteralPath $ConfigPath)) {
    return @()
  }

  $paths = @()
  $lines = @(Get-Content -LiteralPath $ConfigPath -ErrorAction SilentlyContinue)
  foreach ($line in (Get-PinentryProgramLines $lines)) {
    $path = Get-PinentryProgramPathFromLine $line
    if (Test-PinentryPathIsPinentryForWindows $path) {
      $paths += $path
    }
  }

  return @($paths)
}

function Get-InstalledPinentryPaths {
  param(
    [Parameter(Mandatory = $true)][string]$Directory,
    [Parameter(Mandatory = $true)][string]$ConfigPath
  )

  $paths = @()
  $installDirectoryPath = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($Directory))

  foreach ($configuredPath in (Get-ConfiguredPinentryForWindowsPaths -ConfigPath $ConfigPath)) {
    $configuredDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($configuredPath))
    if ( [string]::Equals($configuredDirectory, $installDirectoryPath, [System.StringComparison]::OrdinalIgnoreCase)) {
      $paths += $configuredPath
    }
    else {
      Write-Step "Preserving configured executable outside install directory: $configuredPath."
    }
  }

  if (Test-Path -LiteralPath $Directory) {
    foreach ($file in (Get-ChildItem -LiteralPath $Directory -Filter $PinentryExecutablePattern -File -ErrorAction SilentlyContinue)) {
      if ($file.Name -match $PinentryExecutableRegex) {
        $paths += $file.FullName
      }
    }
  }

  return @($paths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
}

function Remove-InstalledExecutables {
  param([AllowEmptyCollection()][string[]]$Paths = @())

  $removedAny = $false

  foreach ($path in $Paths) {
    if (-not (Test-Path -LiteralPath $path)) {
      Write-Step "Installed executable was not found: $path."
      continue
    }

    Write-Step "Removing installed executable $path."
    Remove-Item -LiteralPath $path -Force
    $removedAny = $true
  }

  if (-not $removedAny) {
    Write-Step 'No installed Pinentry For Windows executable was found to remove.'
  }
}

function Get-BrokerAutostartValue {
  if (-not (Test-Path -LiteralPath $BrokerRunPath)) {
    return $null
  }

  $entry = Get-ItemProperty -LiteralPath $BrokerRunPath -Name $BrokerRunName -ErrorAction SilentlyContinue
  if ($null -eq $entry) {
    return $null
  }

  $property = $entry.PSObject.Properties[$BrokerRunName]
  return $( if ($null -ne $property) {
      $property.Value
    }
    else {
      $null
    } )
}

function Get-OwnedBrokerStartup {
  param(
    [string]$Value,
    [AllowEmptyCollection()][string[]]$InstalledPaths = @()
  )

  if ([string]::IsNullOrWhiteSpace($Value) -or -not $Value.EndsWith('"')) {
    return $null
  }

  foreach ($path in $InstalledPaths) {
    $prefix = '"' + $path + '" --broker "'
    if (-not $Value.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
      continue
    }

    $directory = $Value.Substring($prefix.Length, $Value.Length - $prefix.Length - 1)
    if ([string]::IsNullOrWhiteSpace($directory) -or -not [System.IO.Path]::IsPathFullyQualified($directory)) {
      return $null
    }

    return [pscustomobject]@{
      ExecutablePath  = $path
      BrokerDirectory = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($directory))
      Value           = $Value
    }
  }

  return $null
}

function Remove-OwnedBrokerAutostart {
  param([Parameter(Mandatory = $true)][string]$ExpectedValue)

  if ((Get-BrokerAutostartValue) -cne $ExpectedValue) {
    throw 'Broker autostart changed during uninstall; preserving it for review.'
  }

  Remove-ItemProperty -LiteralPath $BrokerRunPath -Name $BrokerRunName
  Write-Step 'Removed broker autostart owned by this installation.'
}

function Invoke-PinentryCommand {
  param(
    [Parameter(Mandatory = $true)][string]$FilePath,
    [Parameter(Mandatory = $true)][string[]]$Arguments
  )

  & $FilePath @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "Pinentry cleanup command failed with exit code $LASTEXITCODE."
  }
}

function Test-CacheMaterial {
  param([Parameter(Mandatory = $true)][string]$Directory)

  $cache = Join-Path $Directory 'cache'
  return (Test-Path -LiteralPath (Join-Path $cache 'hello.cred')) -or
  ((Test-Path -LiteralPath (Join-Path $cache 'entries')) -and
  @(Get-ChildItem -LiteralPath (Join-Path $cache 'entries') -Filter '*.bin' -File).Count -gt 0)
}

function Test-BrokerMaterial {
  param([Parameter(Mandatory = $true)][string]$Directory)

  return (Test-Path -LiteralPath (Join-Path $Directory 'broker.key.dpapi')) -or
  (Test-Path -LiteralPath (Join-Path $Directory 'broker.cer')) -or
  ((Test-Path -LiteralPath (Join-Path $Directory 'clients')) -and
  @(Get-ChildItem -LiteralPath (Join-Path $Directory 'clients') -Filter '*.origin' -File).Count -gt 0)
}

function Restart-GpgAgent {
  param([Parameter(Mandatory = $true)][string]$GpgConfPath)

  Write-Step 'Restarting gpg-agent so it reads the updated configuration.'
  & $GpgConfPath --kill gpg-agent

  if ($LASTEXITCODE -ne 0) {
    Write-Step 'gpgconf could not kill gpg-agent. Restart it manually if the new pinentry is not used immediately.'
  }
}

function Restart-GpgAgentIfNeeded {
  param(
    [Parameter(Mandatory = $true)][string]$GpgConfPath,
    [Parameter(Mandatory = $true)][bool]$ConfigChanged
  )

  if (-not $ConfigChanged) {
    return
  }

  if ($NoRestartAgent) {
    Write-Step 'Skipping gpg-agent restart because -NoRestartAgent was supplied.'
    return
  }

  Restart-GpgAgent -GpgConfPath $GpgConfPath
}

function Install-LatestRelease {
  param(
    [Parameter(Mandatory = $true)][string]$GpgConfPath,
    [Parameter(Mandatory = $true)][string]$HomeDirectory,
    [Parameter(Mandatory = $true)][string]$InstallDirectory,
    [Parameter(Mandatory = $true)][bool]$IsUpdate
  )

  Enable-Tls12

  $runtimeIdentifiers = Get-WindowsRuntimeIdentifierCandidates
  Write-Step "Compatible release asset types: $( $runtimeIdentifiers -join ', ' )."

  Ensure-InstallDirectory -Directory $InstallDirectory

  $release = Get-LatestRelease
  Write-Step "Latest release is $( $release.tag_name )."

  $asset = Get-ReleaseAsset -Release $release -RuntimeIdentifiers $runtimeIdentifiers
  Write-Step "Selected release asset $( $asset.name )."
  $checksumAsset = Get-ReleaseChecksumAsset -Release $release
  Write-Step "Selected checksum asset $( $checksumAsset.name )."

  $downloadDirectory = Join-Path $InstallDirectory ('.pfw-download-' + [Guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Path $downloadDirectory | Out-Null
  try {
    $temporaryPath = Join-Path $downloadDirectory $asset.name
    $temporaryChecksumPath = Join-Path $downloadDirectory $checksumAsset.name
    Download-Asset -Asset $asset -DestinationPath $temporaryPath
    Download-Asset -Asset $checksumAsset -DestinationPath $temporaryChecksumPath
    Assert-DownloadedAssetChecksum -AssetName $asset.name -AssetPath $temporaryPath -ChecksumPath $temporaryChecksumPath

    $installPath = Join-Path $InstallDirectory $asset.name
    Write-Step "Installing executable to $installPath."
    Move-Item -LiteralPath $temporaryPath -Destination $installPath -Force
  }
  finally {
    Remove-Item -LiteralPath $downloadDirectory -Recurse -Force
  }

  $agentConfigPath = Join-Path $HomeDirectory 'gpg-agent.conf'
  $configChanged = Set-PinentryProgramConfig -ConfigPath $agentConfigPath -PinentryPath $installPath
  Restart-GpgAgentIfNeeded -GpgConfPath $GpgConfPath -ConfigChanged $configChanged

  if ($IsUpdate) {
    Write-Step 'Update complete.'
  }
  else {
    Write-Step 'Installation complete.'
  }
}

function Uninstall-PinentryForWindows {
  param(
    [Parameter(Mandatory = $true)][string]$GpgConfPath,
    [Parameter(Mandatory = $true)][string]$HomeDirectory,
    [Parameter(Mandatory = $true)][string]$InstallDirectory,
    [Parameter(Mandatory = $true)][string]$DataDirectory,
    [string]$BrokerDirectory
  )

  $agentConfigPath = Join-Path $HomeDirectory 'gpg-agent.conf'
  $installedPaths = @(Get-InstalledPinentryPaths -Directory $InstallDirectory -ConfigPath $agentConfigPath)
  $availableExecutables = @($installedPaths | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
  $autostartValue = Get-BrokerAutostartValue
  $ownedStartup = Get-OwnedBrokerStartup -Value $autostartValue -InstalledPaths $availableExecutables
  $resolvedBrokerDirectory = if ($BrokerDirectory) {
    [System.IO.Path]::TrimEndingDirectorySeparator(
      [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($BrokerDirectory)))
  }
  elseif ($null -ne $ownedStartup) {
    $ownedStartup.BrokerDirectory
  }
  else {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'PinentryForWindows\broker'
  }

  if ($null -ne $ownedStartup -and $BrokerDirectory -and
    -not [string]::Equals($resolvedBrokerDirectory, $ownedStartup.BrokerDirectory,
      [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Requested broker directory differs from the owned autostart; preserving both for review.'
  }

  $hasCache = Test-CacheMaterial -Directory $DataDirectory
  $hasBroker = Test-BrokerMaterial -Directory $resolvedBrokerDirectory
  if ($hasBroker -and $autostartValue -and $null -eq $ownedStartup) {
    Write-Step 'Preserving broker identity because autostart belongs to another executable.'
    $hasBroker = $false
  }

  if (($hasCache -or $hasBroker) -and $availableExecutables.Count -eq 0) {
    throw 'No installed Pinentry executable is available to revoke cache or broker material.'
  }

  $configChanged = Remove-PinentryProgramConfig -ConfigPath $agentConfigPath -InstallDirectory $InstallDirectory
  $preferredExecutable = if ($availableExecutables.Count -gt 0) {
    $architecture = @(Get-WindowsRuntimeIdentifierCandidates)[0]
    $availableExecutables | Where-Object {
      (Split-Path -Leaf $_) -eq "pfw.$architecture.exe"
    } | Select-Object -First 1
  }
  $cleanupExecutable = if ($null -ne $ownedStartup) {
    $ownedStartup.ExecutablePath
  }
  elseif ($preferredExecutable) {
    $preferredExecutable
  }
  elseif ($availableExecutables.Count -gt 0) {
    $availableExecutables[0]
  }

  if ($hasCache) {
    Invoke-PinentryCommand -FilePath $cleanupExecutable -Arguments @('--clear-cache', $DataDirectory)
  }

  if ($hasBroker) {
    Invoke-PinentryCommand -FilePath $cleanupExecutable -Arguments @('--reset-broker', $resolvedBrokerDirectory)
  }

  if ($null -ne $ownedStartup) {
    Remove-OwnedBrokerAutostart -ExpectedValue $ownedStartup.Value
  }

  Remove-InstalledExecutables -Paths $installedPaths
  Restart-GpgAgentIfNeeded -GpgConfPath $GpgConfPath -ConfigChanged $configChanged
  Write-Step 'Uninstall complete.'
}

function Assert-Options {
  if ($Update -and $Uninstall) {
    throw 'Use either -Update or -Uninstall, not both.'
  }
}

try {
  Assert-Options

  if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') {
    throw 'This installer must be run on Windows.'
  }

  $gpgConfPath = Get-GpgConfPath
  $binDirectory = Invoke-GpgConfListDir -GpgConfPath $gpgConfPath -Name 'bindir'
  $homeDirectory = Invoke-GpgConfListDir -GpgConfPath $gpgConfPath -Name 'homedir'
  $resolvedInstallDirectory = Resolve-InstallDirectory -RequestedInstallDirectory $InstallDirectory -DefaultInstallDirectory $binDirectory
  $resolvedDataDirectory = if ($DataDirectory) {
    [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($DataDirectory))
  }
  elseif ($env:PFW_DATA_DIR) {
    [System.IO.Path]::GetFullPath($env:PFW_DATA_DIR)
  }
  else {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'PinentryForWindows'
  }

  if (-not $Uninstall -and [string]::IsNullOrWhiteSpace($InstallDirectory) -and -not (Test-Path -LiteralPath $binDirectory)) {
    throw "GnuPG bin directory does not exist: $binDirectory."
  }

  Write-Step "Using GnuPG bin directory $binDirectory."
  Write-Step "Using GnuPG home directory $homeDirectory."
  Write-Step "Using install directory $resolvedInstallDirectory."

  if ($Uninstall) {
    Uninstall-PinentryForWindows -GpgConfPath $gpgConfPath -HomeDirectory $homeDirectory `
      -InstallDirectory $resolvedInstallDirectory -DataDirectory $resolvedDataDirectory -BrokerDirectory $BrokerDirectory
  }
  else {
    Install-LatestRelease -GpgConfPath $gpgConfPath -HomeDirectory $homeDirectory -InstallDirectory $resolvedInstallDirectory -IsUpdate $Update.IsPresent
  }
}
catch {
  Write-Error $_.Exception.Message
  exit 1
}
