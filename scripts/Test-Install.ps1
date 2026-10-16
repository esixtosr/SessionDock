#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$BundlePath = '',
    [switch]$AllowDisposableRunner
)
$ErrorActionPreference = 'Stop'

# This test uses the real per-user installation locations. Never redirect them to
# a substitute path: that would avoid exercising the actual installer contract.
if (-not $AllowDisposableRunner) { throw 'This test requires -AllowDisposableRunner and a disposable GitHub-hosted Windows runner.' }
if ($env:OS -ne 'Windows_NT' -or $env:PROCESSOR_ARCHITECTURE -ne 'AMD64') { throw 'The installation smoke test requires Windows x64.' }
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Run this test only on a disposable GitHub-hosted Windows runner. Self-hosted runners and personal computers are refused.'
}
if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { throw 'LOCALAPPDATA is unavailable.' }

$root = Split-Path $PSScriptRoot -Parent
if (-not $BundlePath) { $BundlePath = Join-Path $root 'artifacts\SessionDock-win-x64' }
$bundle = (Resolve-Path -LiteralPath $BundlePath).ProviderPath.TrimEnd('\')
$installer = Join-Path $bundle 'Install-Local.ps1'
$bundleUninstaller = Join-Path $bundle 'Uninstall-Local.ps1'
$uiScript = Join-Path $PSScriptRoot 'Test-UI.ps1'
foreach ($required in @('SessionDock.exe', 'SessionDock.dll', 'SessionDock.runtimeconfig.json', 'Install-Local.ps1', 'Uninstall-Local.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $bundle $required) -PathType Leaf)) {
        throw "The prepared bundle is missing $required. Run Package-Preview.ps1 after publishing, or pass -BundlePath with a complete bundle."
    }
}
if (-not (Test-Path -LiteralPath $uiScript -PathType Leaf)) { throw 'The existing Test-UI.ps1 script is required.' }

$destination = Join-Path $env:LOCALAPPDATA 'Programs\SessionDock'
$programs = [Environment]::GetFolderPath('Programs')
if ([string]::IsNullOrWhiteSpace($programs)) { throw 'The current user Start menu Programs directory is unavailable.' }
$shortcutPath = Join-Path $programs 'SessionDock.lnk'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SessionDock'
$profileDirectory = Join-Path $env:LOCALAPPDATA 'SessionDock'
foreach ($existing in @($destination, $shortcutPath, $uninstallKey, $profileDirectory)) {
    if (Test-Path -LiteralPath $existing) { throw "Refusing to change an existing installation or user-data location: $existing" }
}
if (Get-Process -Name 'SessionDock' -ErrorAction SilentlyContinue) { throw 'Refusing to run while SessionDock is already running.' }
if ([IO.Path]::GetFullPath($bundle).Equals([IO.Path]::GetFullPath($destination).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The test bundle must be separate from the installation destination.'
}

$out = Join-Path $root 'artifacts\verification'
New-Item -Path $out -ItemType Directory -Force | Out-Null
$report = [ordered]@{
    schemaVersion = 1
    utc = [DateTime]::UtcNow.ToString('O')
    os = [Environment]::OSVersion.Version.ToString()
    runnerEnvironment = $env:RUNNER_ENVIRONMENT
    status = 'Running'
    tests = @()
    realLogin = 'Not tested'
    remoteConnectionAttempted = $false
    credentialVault = 'Not tested or modified by this installation smoke test'
    scope = 'Fresh per-user installation, installed-app UI smoke, uninstall, and retention of a user-data sentinel on a disposable Windows runner. Does not verify upgrades, MSIX, Windows 11 compatibility, or real RDP sessions.'
}
$installationAttempted = $false
$uninstallCompleted = $false
$primaryFailure = $null
$sentinel = Join-Path $profileDirectory 'installation-smoke-sentinel.txt'
$sentinelText = 'SessionDock install test fixture: ' + [Guid]::NewGuid().ToString('N')

function Assert-SamePath([string]$Actual, [string]$Expected, [string]$Description) {
    if ([string]::IsNullOrWhiteSpace($Actual) -or -not ([IO.Path]::GetFullPath($Actual).TrimEnd('\').Equals([IO.Path]::GetFullPath($Expected).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase))) {
        throw "$Description does not point to the expected installation location."
    }
}
function Record-Pass([string]$Name) {
    $report.tests += @{ name = $Name; result = 'Passed' }
    Write-Host "Passed: $Name"
}
function Assert-Removed {
    foreach ($removed in @($destination, $shortcutPath, $uninstallKey)) {
        if (Test-Path -LiteralPath $removed) { throw "Uninstall left an installation item behind: $removed" }
    }
}
function Assert-SentinelRetained {
    if (-not (Test-Path -LiteralPath $sentinel -PathType Leaf) -or [IO.File]::ReadAllText($sentinel) -cne $sentinelText) {
        throw 'The user-data retention sentinel was removed or changed.'
    }
}

try {
    # This is explicitly a retention fixture, not a fabricated database. The UI
    # test below creates real profiles in its own isolated temporary directory.
    New-Item -Path $profileDirectory -ItemType Directory | Out-Null
    [IO.File]::WriteAllText($sentinel, $sentinelText, [Text.UTF8Encoding]::new($false))
    $installationAttempted = $true
    & $installer
    if (-not $?) { throw 'The real per-user installer did not complete successfully.' }

    $sourceFiles = @(Get-ChildItem -LiteralPath $bundle -File -Recurse)
    $installedFiles = @(Get-ChildItem -LiteralPath $destination -File -Recurse)
    if ($sourceFiles.Count -ne $installedFiles.Count) { throw 'The installed file count does not match the prepared bundle.' }
    foreach ($sourceFile in $sourceFiles) {
        $relative = $sourceFile.FullName.Substring($bundle.Length).TrimStart('\')
        $installedFile = Join-Path $destination $relative
        if (-not (Test-Path -LiteralPath $installedFile -PathType Leaf)) { throw "An installed file is missing: $relative" }
        if ((Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath $installedFile -Algorithm SHA256).Hash) {
            throw "An installed file differs from the prepared bundle: $relative"
        }
    }
    Record-Pass 'Complete installed bundle matches source file hashes'

    if (-not (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) { throw 'The Start menu shortcut was not created.' }
    $shell = $null
    $shortcut = $null
    try {
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($shortcutPath)
        Assert-SamePath $shortcut.TargetPath (Join-Path $destination 'SessionDock.exe') 'Start menu shortcut target'
        Assert-SamePath $shortcut.WorkingDirectory $destination 'Start menu shortcut working directory'
    } finally {
        if ($null -ne $shortcut) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) }
        if ($null -ne $shell) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    }
    Record-Pass 'Start menu shortcut points to the installed application'

    $registration = Get-ItemProperty -LiteralPath $uninstallKey
    Assert-SamePath $registration.InstallLocation $destination 'Installed Apps location'
    if ($registration.DisplayName -ne 'SessionDock Preview' -or [string]::IsNullOrWhiteSpace($registration.DisplayVersion)) {
        throw 'Installed Apps registration is missing its name or version.'
    }
    $installedUninstaller = Join-Path $destination 'Uninstall-Local.ps1'
    $expectedUninstall = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + $installedUninstaller + '"'
    if ($registration.UninstallString -cne $expectedUninstall) { throw 'Installed Apps does not register the bundled uninstaller correctly.' }
    if ($registration.NoModify -ne 1 -or $registration.NoRepair -ne 1) { throw 'Installed Apps modify/repair flags do not match the installer.' }
    Record-Pass 'Installed Apps entry registers the bundled uninstaller'
    Assert-SentinelRetained
    Record-Pass 'Installation preserves existing user-data fixture'

    # Test-UI writes ui-smoke.json. Keep an earlier published-build report intact
    # and save this invocation separately so portable and installed evidence differ.
    $uiReportPath = Join-Path $out 'ui-smoke.json'
    $installedUiReportPath = Join-Path $out 'installed-ui-smoke.json'
    $priorUiReport = $null
    $hadPriorUiReport = Test-Path -LiteralPath $uiReportPath -PathType Leaf
    if ($hadPriorUiReport) {
        $priorUiReport = [IO.File]::ReadAllBytes($uiReportPath)
        Remove-Item -LiteralPath $uiReportPath
    }
    try {
        & $uiScript -Executable (Join-Path $destination 'SessionDock.exe')
        if (-not $?) { throw 'The installed-app UI test did not complete successfully.' }
        if (-not (Test-Path -LiteralPath $uiReportPath -PathType Leaf)) { throw 'The installed-app UI test produced no verification report.' }
        $uiResult = Get-Content -LiteralPath $uiReportPath -Raw | ConvertFrom-Json
        if ($uiResult.status -ne 'Passed' -or $uiResult.realLogin -ne 'Not tested' -or $uiResult.networkAccess -ne $false) {
            throw 'The installed-app UI report did not confirm a passing no-connection smoke test.'
        }
        Record-Pass 'Installed executable passes isolated UI save, cancel, restart, and settings checks'
    } finally {
        if (Test-Path -LiteralPath $uiReportPath -PathType Leaf) { Copy-Item -LiteralPath $uiReportPath -Destination $installedUiReportPath -Force }
        if ($hadPriorUiReport) { [IO.File]::WriteAllBytes($uiReportPath, $priorUiReport) }
    }

    # Invoke the actual installed script. Do not evaluate a registry command string.
    & $installedUninstaller
    if (-not $?) { throw 'The real uninstaller did not complete successfully.' }
    Assert-Removed
    $uninstallCompleted = $true
    Record-Pass 'Uninstall removes program files, Start menu shortcut, and Installed Apps registration'
    Assert-SentinelRetained
    Record-Pass 'Uninstall retains user-data fixture unchanged'
    $report.status = 'Passed'
} catch {
    $primaryFailure = $_
    $report.status = 'Failed'
    $report.failure = $_.Exception.Message
} finally {
    if ($installationAttempted -and -not $uninstallCompleted) {
        try {
            # The first attempt may have failed before copying the installed
            # uninstaller. The identical bundle script still uses the real path.
            & $bundleUninstaller
            if (-not $?) { throw 'Cleanup uninstaller did not complete successfully.' }
            Assert-Removed
            Assert-SentinelRetained
            $report.cleanup = 'Installation remnants removed; user-data fixture retained'
        } catch {
            $report.cleanup = 'Failed'
            $report.cleanupFailure = $_.Exception.Message
            $report.status = 'Failed'
            if ($null -eq $primaryFailure) { $primaryFailure = $_ }
        }
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'install-smoke.json') -Encoding UTF8
    Write-Host "Install report: $out\install-smoke.json"
    Write-Host "Retention fixture left at $sentinel. The disposable runner removes it when the job ends."
}
if ($null -ne $primaryFailure) { throw $primaryFailure }
Write-Output 'Per-user installation, installed-app UI, and uninstall smoke checks passed. No remote connection was attempted. Upgrades, MSIX, Windows 11 compatibility, and real RDP still need separate verification.'
