[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\SessionDock'
if (Get-Process -Name 'SessionDock' -ErrorAction SilentlyContinue) { throw 'Exit SessionDock and its tray icon before uninstalling.' }
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'SessionDock.lnk') -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SessionDock' -Recurse -ErrorAction SilentlyContinue
if (Test-Path $destination) { Remove-Item $destination -Recurse -Force }
Write-Host 'SessionDock was removed. Your library, backups and app-owned saved credentials were retained for reinstall. Delete saved credentials from SessionDock Settings before uninstalling if you want to remove them.'
