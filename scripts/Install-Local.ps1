[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'This installer requires Windows.' }
if ($env:PROCESSOR_ARCHITECTURE -ne 'AMD64') { throw 'This preview targets Windows x64. ARM64 compatibility is unverified.' }
$source = $PSScriptRoot
if (-not (Test-Path (Join-Path $source 'SessionDock.exe'))) { throw 'Run Install.cmd from the extracted published Windows bundle, next to SessionDock.exe.' }
$destination = Join-Path $env:LOCALAPPDATA 'Programs\SessionDock'
if ([IO.Path]::GetFullPath($source).TrimEnd('\') -eq [IO.Path]::GetFullPath($destination).TrimEnd('\')) { throw 'Extract the new bundle to a separate folder before installing or upgrading.' }
if (Get-Process -Name 'SessionDock' -ErrorAction SilentlyContinue) { throw 'Exit SessionDock, including its tray icon and session windows, then run the installer again.' }
New-Item $destination -ItemType Directory -Force | Out-Null
Get-ChildItem $source | Copy-Item -Destination $destination -Recurse -Force
$programs = [Environment]::GetFolderPath('Programs')
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $programs 'SessionDock.lnk'))
$shortcut.TargetPath = Join-Path $destination 'SessionDock.exe'
$shortcut.WorkingDirectory = $destination
$shortcut.Description = 'SessionDock — saved PC connections (preview)'
$shortcut.Save()
$uninstall = Join-Path $destination 'Uninstall-Local.ps1'
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SessionDock'
New-Item $key -Force | Out-Null
New-ItemProperty $key -Name DisplayName -Value 'SessionDock Preview' -PropertyType String -Force | Out-Null
New-ItemProperty $key -Name DisplayVersion -Value '0.1.0-preview.1' -PropertyType String -Force | Out-Null
New-ItemProperty $key -Name Publisher -Value 'SessionDock contributors' -PropertyType String -Force | Out-Null
New-ItemProperty $key -Name InstallLocation -Value $destination -PropertyType String -Force | Out-Null
New-ItemProperty $key -Name UninstallString -Value ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + $uninstall + '"') -PropertyType String -Force | Out-Null
New-ItemProperty $key -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty $key -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
Write-Host 'Installed for this Windows account. Open SessionDock from Start. No startup task, firewall rule or certificate trust was added.'
