param(
  [string]$ExecutablePath,
  [string]$BrokerDirectory = (Join-Path $env:LOCALAPPDATA 'PinentryForWindows\broker'),
  [string]$Distribution,
  [string]$RelayCertificatePath,
  [switch]$NoAutostart,
  [switch]$RemoveAutostart,
  [switch]$RenewBroker,
  [switch]$ReplacePairing
)

$ErrorActionPreference = 'Stop'
$ServiceId = '0000AFBA-FACB-11E6-BD58-64006A7986D3'
$RunPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$RunName = 'PinentryForWindowsBroker'

function Convert-BridgeGpgPath
{
  param([Parameter(Mandatory = $true)][string]$Path)

  $normalized = $Path.Trim()
  if ($normalized -match '^([A-Za-z])%3[aA][\\/](.*)$')
  {
    $normalized = $Matches[1] + ':\' + $Matches[2]
  }
  elseif ($normalized -match '^/([A-Za-z])/(.*)$')
  {
    $normalized = $Matches[1] + ':\' + $Matches[2]
  }

  return [System.Uri]::UnescapeDataString(($normalized -replace '/', '\'))
}

if ([string]::IsNullOrWhiteSpace($ExecutablePath))
{
  $gpgConf = Get-Command gpgconf -ErrorAction SilentlyContinue
  if ($null -eq $gpgConf)
  {
    throw 'gpgconf was not found; pass -ExecutablePath for a custom installation.'
  }

  $bindir = & $gpgConf.Source --list-dirs bindir
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($bindir))
  {
    throw 'gpgconf did not return a bindir; pass -ExecutablePath for a custom installation.'
  }

  $directory = Convert-BridgeGpgPath $bindir
  if (-not [System.IO.Path]::IsPathFullyQualified($directory))
  {
    throw 'gpgconf returned an invalid bindir; pass -ExecutablePath for a custom installation.'
  }

  $architecture = if ($env:PROCESSOR_ARCHITEW6432)
  {
    $env:PROCESSOR_ARCHITEW6432
  }
  else
  {
    $env:PROCESSOR_ARCHITECTURE
  }
  $runtime = switch ($architecture.ToUpperInvariant())
  {
    'AMD64' { 'x64' }
    'ARM64' { 'arm64' }
    default { throw "Unsupported Windows architecture: $architecture" }
  }
  $ExecutablePath = Join-Path $directory "pfw.$runtime.exe"
}

$ExecutablePath = [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($ExecutablePath))
$BrokerDirectory = [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($BrokerDirectory))

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf))
{
  throw "Pinentry executable not found: $ExecutablePath"
}

if ($RemoveAutostart)
{
  if (Test-Path $RunPath)
  {
    $current = (Get-ItemProperty -Path $RunPath -Name $RunName -ErrorAction SilentlyContinue).$RunName
    if ($current -and $current.StartsWith('"' + $ExecutablePath + '" --broker ', [StringComparison]::OrdinalIgnoreCase))
    {
      Remove-ItemProperty -Path $RunPath -Name $RunName
      Write-Host 'Broker autostart removed.'
    }
  }

  exit 0
}

if ($ReplacePairing -and -not $RelayCertificatePath)
{
  throw '-ReplacePairing requires -RelayCertificatePath and -Distribution.'
}

& $ExecutablePath --init-broker $BrokerDirectory
if ($LASTEXITCODE -ne 0)
{
  throw 'Broker identity initialization failed.'
}

if ($RenewBroker)
{
  & $ExecutablePath --renew-broker $BrokerDirectory
  if ($LASTEXITCODE -ne 0)
  {
    throw 'Broker certificate renewal failed.'
  }

  Write-Host 'Restart the broker, then pin the new certificate in each WSL relay with --broker-certificate <path> --replace-broker.'
}

if ($RelayCertificatePath)
{
  if (-not $Distribution -or $Distribution -notmatch '^[A-Za-z0-9._-]+$')
  {
    throw 'A distribution name containing only letters, digits, dot, underscore or hyphen is required.'
  }

  $RelayCertificatePath = [System.IO.Path]::GetFullPath($RelayCertificatePath)
  if (-not (Test-Path -LiteralPath $RelayCertificatePath -PathType Leaf))
  {
    throw "Relay public certificate not found: $RelayCertificatePath"
  }

  $pairingCommand = if ($ReplacePairing) { '--replace-client' } else { '--approve-client' }
  & $ExecutablePath $pairingCommand $BrokerDirectory $RelayCertificatePath "wsl:$Distribution"
  if ($LASTEXITCODE -ne 0)
  {
    throw 'Relay pairing failed.'
  }
}

if (-not $NoAutostart)
{
  if (-not (Test-Path $RunPath))
  {
    New-Item -Path $RunPath -Force | Out-Null
  }
  $value = '"' + $ExecutablePath + '" --broker "' + $BrokerDirectory + '"'
  $current = (Get-ItemProperty -Path $RunPath -Name $RunName -ErrorAction SilentlyContinue).$RunName
  if ($current -and $current -ne $value)
  {
    throw 'An existing broker autostart points elsewhere. Review it before replacement.'
  }

  Set-ItemProperty -Path $RunPath -Name $RunName -Value $value
  Write-Host 'Broker autostart configured for this Windows user.'
}

Write-Host 'Run the following command in an elevated Command Prompt to register the Hyper-V service:'
Write-Host ('reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Virtualization\GuestCommunicationServices\' +
  $ServiceId + '" /v ElementName /t REG_SZ /d "PinentryForWindows Broker" /f')
Write-Host ('Broker public certificate for the WSL relay: ' + (Join-Path $BrokerDirectory 'broker.cer'))
Write-Host ('Start the broker for this session with: "' + $ExecutablePath + '" --broker "' + $BrokerDirectory + '"')
