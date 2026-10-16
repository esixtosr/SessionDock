[CmdletBinding()]
param([switch]$SkipInteropProbe)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
if ($env:OS -ne 'Windows_NT' -or -not [Environment]::Is64BitOperatingSystem) { throw 'Run this script on Windows 11 x64.' }
if ($env:PROCESSOR_ARCHITECTURE -ne 'AMD64') { throw 'Use an x64 PowerShell session. ARM64 is not yet validated.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 10 SDK listed in global.json, then reopen PowerShell.' }
$report = Join-Path $root 'artifacts/windows-environment.txt'
New-Item (Split-Path $report) -ItemType Directory -Force | Out-Null
@("SessionDock Windows build", "UTC: $([DateTime]::UtcNow.ToString('O'))", "OS: $([Environment]::OSVersion.Version)", "Architecture: $env:PROCESSOR_ARCHITECTURE") | Set-Content $report
dotnet --info | Add-Content $report
if ($LASTEXITCODE -ne 0) { throw 'The pinned SDK was not found. Read global.json and install that SDK; do not silently change the pin.' }
dotnet restore SessionDock.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
dotnet build SessionDock.slnx -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
dotnet test tests/SessionDock.Tests/SessionDock.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=portable-windows.trx' --results-directory artifacts/TestResults
if ($LASTEXITCODE -ne 0) { throw 'Logic or persistence tests failed.' }
if (-not $SkipInteropProbe) {
    & (Join-Path $PSScriptRoot 'Generate-Interop.ps1')
    if (-not $?) { throw 'Interop generation or validation failed.' }
    & (Join-Path $PSScriptRoot 'Test-Engine.ps1')
    if (-not $?) { throw 'Windows control or vault probe failed.' }
} else { 'Interop probe explicitly skipped: phase 0 remains unverified.' | Add-Content $report }
dotnet publish src/SessionDock.App/SessionDock.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/SessionDock-win-x64
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item scripts/Install-Local.ps1,scripts/Uninstall-Local.ps1,scripts/Install.cmd -Destination artifacts/SessionDock-win-x64
Copy-Item README.md,THIRD-PARTY-NOTICES.md,START-HERE.txt -Destination artifacts/SessionDock-win-x64
Copy-Item docs,licenses -Destination artifacts/SessionDock-win-x64 -Recurse -Force
'Build and automated tests completed. Interactive RDP, UI and installation tests are NOT proven by this script. Follow docs/WINDOWS-ACCEPTANCE.md.'
