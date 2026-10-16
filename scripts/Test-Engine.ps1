#requires -Version 5.1
[CmdletBinding()]
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne 'Win32NT') { throw 'Windows is required; no engine validation was performed.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$report = Join-Path $root 'artifacts/verification/engine-capabilities.json'
& dotnet run --project (Join-Path $root 'tests/SessionDock.WindowsTests/SessionDock.WindowsTests.csproj') -c $Configuration -- --report $report
if ($LASTEXITCODE -ne 0) { throw 'The installed-control activation probe failed. See its redacted output and local capability report. No real-session test was run. The vault probe writes and removes one temporary app-owned item.' }
Write-Output "Activation and vault report: $report. Next run the authorized interactive Windows acceptance checks in docs/ENGINE.md."
