[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'MSIX packaging requires Windows with Visual Studio and the packaging tools.' }
$root = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'Install Visual Studio with .NET desktop development and Windows Application Packaging Project support.' }
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild was not found.' }
& $msbuild (Join-Path $root 'SessionDock.Package\SessionDock.Package.wapproj') /restore /p:Configuration=Release /p:Platform=x64 /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxBundle=Never /p:AppxPackageSigningEnabled=false
if ($LASTEXITCODE -ne 0) { throw 'MSIX packaging failed. Check the Windows SDK and packaging project workload.' }
Write-Host 'Unsigned MSIX created. It is not a distributable signed release. Sign with an appropriate external certificate and follow the clean-machine acceptance tests. This script does not create or trust certificates.'
