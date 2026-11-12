<#
Checks no-argument bridge bootstrap from a streamed script using a disposable
gpgconf. Missing pinentry must be reported before broker or registry changes.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$root = Join-Path $repo 'artifacts' ('bridge-setup-test-' + [Guid]::NewGuid().ToString('N'))
$tools = Join-Path $root 'fake-bin'
$bindir = Join-Path $root 'gnupg-bin'
$broker = Join-Path $root 'broker'
$oldPath = $env:PATH
$oldLocalAppData = $env:LOCALAPPDATA
$oldBindir = $env:PFW_TEST_BINDIR

try {
  New-Item -ItemType Directory -Path $tools, $bindir -Force | Out-Null
  [System.IO.File]::WriteAllLines((Join-Path $tools 'gpgconf.cmd'), @(
      '@echo off'
      'if "%1" == "--list-dirs" if "%2" == "bindir" ('
      '  echo %PFW_TEST_BINDIR%'
      '  exit /b 0'
      ')'
      'exit /b 2'
    ))

  $env:PATH = "$tools;$oldPath"
  $env:LOCALAPPDATA = $root
  $env:PFW_TEST_BINDIR = $bindir -replace '^([A-Za-z]):', '$1%3a'
  $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
  $asset = switch ($architecture) {
    'X64' {
      'pfw.x64.exe'
    }
    'Arm64' {
      'pfw.arm64.exe'
    }
    default {
      throw "Unexpected test architecture: $architecture"
    }
  }
  $expected = Join-Path $bindir $asset
  $source = [System.IO.File]::ReadAllText((Join-Path $repo 'tools/setup-bridge.ps1'))

  $failure = $null
  try {
    $source | Invoke-Expression
  }
  catch {
    $failure = $_.Exception.Message
  }
  if ($failure -ne "Pinentry executable not found: $expected") {
    throw "Bridge did not reject the missing decoded path: $failure"
  }

  $env:PFW_TEST_BINDIR = 'relative-gnupg-bin'
  $failure = $null
  try {
    $source | Invoke-Expression
  }
  catch {
    $failure = $_.Exception.Message
  }
  if ($failure -notlike 'gpgconf returned an invalid bindir*') {
    throw "Bridge accepted a relative gpgconf path: $failure"
  }

  $custom = Join-Path $root 'custom\pfw.x64.exe'
  $failure = $null
  try {
    & ([scriptblock]::Create($source)) -ExecutablePath $custom -Distribution archlinux `
      -RelayCertificatePath (Join-Path $root 'client.cer') -NoAutostart
  }
  catch {
    $failure = $_.Exception.Message
  }
  if ($failure -ne "Pinentry executable not found: $custom") {
    throw "Explicit executable path did not bypass gpgconf discovery: $failure"
  }

  if (Test-Path -LiteralPath $broker) {
    throw 'Bridge created broker material before validating the executable.'
  }
  Write-Host 'Streamed bridge bootstrap decoded gpgconf bindir and failed closed: PASS'
}
finally {
  $env:PATH = $oldPath
  $env:LOCALAPPDATA = $oldLocalAppData
  $env:PFW_TEST_BINDIR = $oldBindir
  if (Test-Path -LiteralPath $root) {
    Remove-Item -LiteralPath $root -Recurse -Force
  }
}
