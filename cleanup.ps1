#!/usr/bin/env pwsh

param(
  [switch] $All,
  [switch] $Modified
)

if ($All -and $Modified) {
  Write-Error "Cannot use -All and -Modified together."
  exit 1
}

if (-not $All -and -not $Modified) {
  Write-Error "Specify -All or -Modified."
  exit 1
}

if ($All) {
  & dotnet jb cleanupcode PinentryForWindows.slnx --profile="Built-in: Full Cleanup"
}
else {
  $files = git status --porcelain=v1 |
    ForEach-Object { $_.Substring(3).Trim() } |
    Where-Object { (Test-Path $_) -and ($_ -match '\.(cs|csproj|props|targets|json|xml)$') }

  if (-not $files) {
    Write-Host "No modified files to clean up."
    exit 0
  }

  $include = $files -join ";"
  & dotnet jb cleanupcode PinentryForWindows.slnx --profile="Built-in: Full Cleanup" "--include=$include"
}
