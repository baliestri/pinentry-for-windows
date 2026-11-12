<#
Measures GETPIN dispatch to the native Windows Security API call and the UI
helper's resource use. Cancel each prompt; no passphrase is requested or recorded.
#>

[CmdletBinding()]
param(
  [int]$Samples = 10,
  [string]$ExecutablePath = (Join-Path $PSScriptRoot '..\artifacts\release-check-win-x64\pfw.exe')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Get-Median {
  param([double[]]$Sorted)
  $middle = [int][Math]::Floor($Sorted.Count / 2)
  if ($Sorted.Count % 2 -eq 0) {
    return ($Sorted[$middle - 1] + $Sorted[$middle]) / 2
  }
  return $Sorted[$middle]
}

if ($Samples -lt 2 -or $Samples -gt 30) {
  throw 'Samples must be between 2 and 30.'
}
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
  throw "Published Windows executable not found: $ExecutablePath"
}

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $repo 'artifacts'
if (-not (Test-Path -LiteralPath $artifacts -PathType Container)) {
  throw 'Repository artifacts directory is missing.'
}

$directory = Join-Path $artifacts ('latency-test-' + [Guid]::NewGuid().ToString('N'))
$logPath = Join-Path $directory 'samples.txt'
$helperPath = Join-Path $directory 'helper.csv'
New-Item -ItemType Directory -Path $directory | Out-Null
try {
  for ($index = 1; $index -le $Samples; $index++) {
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = [System.IO.Path]::GetFullPath($ExecutablePath)
    $start.WorkingDirectory = $repo
    $start.UseShellExecute = $false
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardInputEncoding = [System.Text.UTF8Encoding]::new($false)
    $start.Environment['PFW_DATA_DIR'] = $directory
    $start.Environment['PFW_LATENCY_LOG'] = $logPath
    $start.Environment['PFW_HELPER_METRICS_LOG'] = $helperPath

    Write-Host "Prompt $index/$Samples — press Cancel in Windows Security." -ForegroundColor Yellow
    $process = [System.Diagnostics.Process]::Start($start)
    try {
      $greeting = $process.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(10)).GetAwaiter().GetResult()
      if ($greeting -notmatch '^OK\b') {
        throw 'Pinentry did not return an Assuan greeting.'
      }

      foreach ($command in @('SETTIMEOUT 120', 'SETTITLE Client%20supplied%20title',
          'SETDESC Latency measurement: please press Cancel')) {
        $process.StandardInput.WriteLine($command)
        $reply = $process.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(10)).GetAwaiter().GetResult()
        if ($reply -notmatch '^OK\b') {
          throw "Unexpected Assuan response for $command"
        }
      }

      $process.StandardInput.WriteLine('GETPIN')
      $response = $process.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(130)).GetAwaiter().GetResult()
      if ($response -notmatch '^ERR 83886179\b') {
        throw "Expected cancellation in prompt $index; do not enter a passphrase."
      }

      $process.StandardInput.WriteLine('BYE')
      $bye = $process.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(10)).GetAwaiter().GetResult()
      if ($bye -notmatch '^OK\b' -or -not $process.WaitForExit(10000)) {
        throw 'Pinentry did not exit cleanly.'
      }
      if (@(Get-Content -LiteralPath $logPath).Count -ne $index) {
        throw 'The UI helper did not write exactly one latency sample.'
      }
      if (@(Get-Content -LiteralPath $helperPath).Count -ne $index) {
        throw 'The UI helper did not write exactly one resource sample.'
      }
    }
    finally {
      if (-not $process.HasExited) {
        $process.Kill($true)
      }
      $process.Dispose()
    }
  }

  $values = @(Get-Content -LiteralPath $logPath | ForEach-Object {
      [double]::Parse($_, [System.Globalization.CultureInfo]::InvariantCulture)
    })
  $warm = @($values | Select-Object -Skip 1 | Sort-Object)
  $median = Get-Median -Sorted $warm
  $p95 = $warm[[int][Math]::Ceiling($warm.Count * 0.95) - 1]

  Write-Host "First sample: $($values[0].ToString('F2') ) ms"
  Write-Host "Warm median: $($median.ToString('F2') ) ms"
  Write-Host "Warm p95: $($p95.ToString('F2') ) ms ($( $warm.Count ) samples)"

  $resources = @(Get-Content -LiteralPath $helperPath | ForEach-Object {
      $columns = $_.Split(',')
      if ($columns.Length -ne 3) {
        throw 'Invalid helper resource sample.'
      }
      [pscustomobject]@{
        AllocatedKiB   = [double]::Parse($columns[0], [System.Globalization.CultureInfo]::InvariantCulture) / 1KB
        PeakWorkingMiB = [double]::Parse($columns[1], [System.Globalization.CultureInfo]::InvariantCulture) / 1MB
        PrivateMiB     = [double]::Parse($columns[2], [System.Globalization.CultureInfo]::InvariantCulture) / 1MB
      }
    })
  $allocated = @($resources | Select-Object -Skip 1 | ForEach-Object AllocatedKiB | Sort-Object)
  $working = @($resources | Select-Object -Skip 1 | ForEach-Object PeakWorkingMiB | Sort-Object)
  $private = @($resources | Select-Object -Skip 1 | ForEach-Object PrivateMiB | Sort-Object)
  Write-Host "Warm helper allocation median: $((Get-Median -Sorted $allocated).ToString('F1') ) KiB"
  Write-Host "Warm helper peak working-set median: $((Get-Median -Sorted $working).ToString('F1') ) MiB"
  Write-Host "Warm helper private-memory median: $((Get-Median -Sorted $private).ToString('F1') ) MiB"
}
finally {
  Remove-Item -LiteralPath $directory -Recurse -Force
}
