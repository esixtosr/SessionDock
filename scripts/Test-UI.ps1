#requires -Version 5.1
[CmdletBinding()]
param([string]$Executable = '', [switch]$CaptureScreenshots)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows with an interactive desktop is required.' }
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$root = Split-Path $PSScriptRoot -Parent
if (-not $Executable) { $Executable = Join-Path $root 'artifacts\SessionDock-win-x64\SessionDock.exe' }
if (-not (Test-Path $Executable)) { throw 'Build the application first or provide -Executable with the full path to SessionDock.exe.' }
$testData = Join-Path ([IO.Path]::GetTempPath()) ('SessionDock-UiTest-' + [Guid]::NewGuid().ToString('N'))
$script:process = $null
$report = [ordered]@{ schemaVersion=1; utc=[DateTime]::UtcNow.ToString('O'); os=[Environment]::OSVersion.Version.ToString(); tests=@(); realLogin='Not tested'; networkAccess=$false; screenshotsRequested=[bool]$CaptureScreenshots; screenshots=@(); screenshotWarnings=@() }
function Find-Element([System.Windows.Automation.AutomationElement]$Parent, [string]$Name, [System.Windows.Automation.ControlType]$Type) {
    $conditions = [System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name),
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $Type))
    $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $conditions)
}
function Wait-Window([string]$Title) {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $condition = [System.Windows.Automation.AndCondition]::new([System.Windows.Automation.Condition[]]@(
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$script:process.Id),
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Title),
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)))
        # WPF owned modal windows can be below their owner in the UIA tree.
        # Keep exact process, title, and window-type checks while finding them.
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($window) { return $window }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for '$Title'."
}
function Invoke-Button($Window,[string]$Name) {
    $button = Find-Element $Window $Name ([System.Windows.Automation.ControlType]::Button)
    if (-not $button) { throw "Button '$Name' was not found." }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Set-Field($Window,[string]$Name,[string]$Value) {
    $field = Find-Element $Window $Name ([System.Windows.Automation.ControlType]::Edit)
    if (-not $field) { throw "Field '$Name' was not found." }
    $field.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Value)
}
function Select-SettingsPage($Window, [string]$Name) {
    $item = Find-Element $Window $Name ([System.Windows.Automation.ControlType]::ListItem)
    if (-not $item) { throw "Missing settings category '$Name'." }
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        if (Find-Element $Window $Name ([System.Windows.Automation.ControlType]::Text)) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Settings category '$Name' did not finish loading."
}
function Save-AppScreenshot([System.Windows.Automation.AutomationElement]$Window, [string]$Name) {
    if (-not $CaptureScreenshots) { return }
    $bitmap = $null
    $graphics = $null
    $previousDpi = [IntPtr]::Zero
    try {
        if ($Name -notin @('library', 'settings', 'about', 'failure')) { throw 'Only known isolated-test windows may be captured.' }
        if ($Window.Current.ProcessId -ne $script:process.Id) { throw 'The capture window does not belong to the isolated test application.' }
        Add-Type -AssemblyName System.Drawing
        if (-not ('SessionDock.UiSmokeCapture' -as [type])) {
            Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace SessionDock {
    public static class UiSmokeCapture {
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmFlush();
        // GW_HWNDPREV walks windows above the target in stacking order. Refuse
        // overlapping visible windows, and bound traversal if the desktop changes.
        public static bool IsUnobscured(IntPtr target, int left, int top, int right, int bottom) {
            int count = 0;
            var seen = new System.Collections.Generic.HashSet<IntPtr>();
            for (IntPtr hwnd = GetWindow(target, 3); hwnd != IntPtr.Zero; hwnd = GetWindow(hwnd, 3)) {
                if (++count > 512 || !seen.Add(hwnd)) return false;
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) continue;
                int cloaked;
                if (DwmGetWindowAttribute(hwnd, 14, out cloaked, 4) == 0 && cloaked != 0) continue;
                Rect other;
                if (GetWindowRect(hwnd, out other) && other.Right > left && other.Left < right && other.Bottom > top && other.Top < bottom) return false;
            }
            return true;
        }
    }
}
'@
        }
        $handle = [IntPtr]$Window.Current.NativeWindowHandle
        if ($handle -eq [IntPtr]::Zero) { throw 'The app window has no native handle.' }
        $pattern = $Window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
        $pattern.SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Normal)
        [void][SessionDock.UiSmokeCapture]::SetForegroundWindow($handle)
        $deadline = [DateTime]::UtcNow.AddSeconds(2)
        while ([SessionDock.UiSmokeCapture]::GetForegroundWindow() -ne $handle -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 100
        }
        if ([SessionDock.UiSmokeCapture]::GetForegroundWindow() -ne $handle -or -not [SessionDock.UiSmokeCapture]::IsWindowVisible($handle) -or [SessionDock.UiSmokeCapture]::IsIconic($handle)) {
            throw 'The app window could not be made visible and foreground; no image was captured.'
        }
        # UI Automation bounds are screen pixels. Temporarily use physical-pixel
        # metrics for this thread so scaling cannot widen the capture to desktop.
        $previousDpi = [SessionDock.UiSmokeCapture]::SetThreadDpiAwarenessContext([IntPtr](-4))
        if ($previousDpi -eq [IntPtr]::Zero) { throw 'Physical-pixel capture context is unavailable.' }
        Start-Sleep -Milliseconds 150
        [void][SessionDock.UiSmokeCapture]::DwmFlush()
        $bounds = $Window.Current.BoundingRectangle
        if ($bounds.IsEmpty -or [double]::IsNaN($bounds.Width) -or [double]::IsInfinity($bounds.Width) -or $bounds.Width -le 0 -or $bounds.Height -le 0) {
            throw 'The app window has no valid visible bounds.'
        }
        $virtualLeft = [SessionDock.UiSmokeCapture]::GetSystemMetrics(76)
        $virtualTop = [SessionDock.UiSmokeCapture]::GetSystemMetrics(77)
        $virtualRight = $virtualLeft + [SessionDock.UiSmokeCapture]::GetSystemMetrics(78)
        $virtualBottom = $virtualTop + [SessionDock.UiSmokeCapture]::GetSystemMetrics(79)
        # Round inward and intersect the virtual screen: never include pixels
        # outside the tested app window, even if a window is partly off-screen.
        $left = [int][Math]::Max([Math]::Ceiling($bounds.Left), $virtualLeft)
        $top = [int][Math]::Max([Math]::Ceiling($bounds.Top), $virtualTop)
        $right = [int][Math]::Min([Math]::Floor($bounds.Right), $virtualRight)
        $bottom = [int][Math]::Min([Math]::Floor($bounds.Bottom), $virtualBottom)
        $width = $right - $left
        $height = $bottom - $top
        if ($width -le 0 -or $height -le 0 -or ([long]$width * $height) -gt 33554432) { throw 'The clipped window bounds are empty or too large.' }
        if (-not [SessionDock.UiSmokeCapture]::IsUnobscured($handle, $left, $top, $right, $bottom)) { throw 'Another window overlaps the app; no image was captured.' }
        $bitmap = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.CopyFromScreen($left, $top, 0, 0, [System.Drawing.Size]::new($width, $height), [System.Drawing.CopyPixelOperation]::SourceCopy)
        $after = $Window.Current.BoundingRectangle
        if ([SessionDock.UiSmokeCapture]::GetForegroundWindow() -ne $handle -or -not $bounds.Equals($after) -or -not [SessionDock.UiSmokeCapture]::IsUnobscured($handle, $left, $top, $right, $bottom)) {
            throw 'The window moved, lost focus, or became obscured during capture; the image was discarded.'
        }
        $screenshotDirectory = Join-Path $root 'artifacts\verification\screenshots'
        New-Item -Path $screenshotDirectory -ItemType Directory -Force | Out-Null
        $imagePath = Join-Path $screenshotDirectory ($Name + '.png')
        $bitmap.Save($imagePath, [System.Drawing.Imaging.ImageFormat]::Png)
        $report.screenshots += @{ name=$Name; result='Captured'; file=('screenshots/' + $Name + '.png'); width=$width; height=$height; syntheticData=$true }
        Write-Host "Captured synthetic app window: $imagePath"
    } catch {
        $warning = "Optional $Name screenshot skipped: $($_.Exception.Message)"
        $report.screenshotWarnings += $warning
        Write-Host $warning
    } finally {
        try { if ($null -ne $graphics) { $graphics.Dispose() } } catch { }
        try { if ($null -ne $bitmap) { $bitmap.Dispose() } } catch { }
        try { if ($previousDpi -ne [IntPtr]::Zero) { [void][SessionDock.UiSmokeCapture]::SetThreadDpiAwarenessContext($previousDpi) } } catch { }
    }
}
function Save-UiFailureDiagnostics {
    try {
        if ($null -eq $script:process) { return }
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$script:process.Id)
        $elements = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        $items = @()
        $windows = @()
        $count = [Math]::Min($elements.Count, 300)
        for ($i = 0; $i -lt $count; $i++) {
            try {
                $element = $elements.Item($i)
                $current = $element.Current
                $items += [ordered]@{
                    name = $current.Name
                    controlType = $current.ControlType.ProgrammaticName
                    nativeHandle = $current.NativeWindowHandle
                    enabled = $current.IsEnabled
                    offscreen = $current.IsOffscreen
                    bounds = $current.BoundingRectangle.ToString()
                }
                if ($current.ControlType -eq [System.Windows.Automation.ControlType]::Window) { $windows += $element }
            } catch { }
        }
        $diagnosticDirectory = Join-Path $root 'artifacts\verification'
        New-Item -Path $diagnosticDirectory -ItemType Directory -Force | Out-Null
        [ordered]@{ processId=$script:process.Id; totalElements=$elements.Count; truncated=($elements.Count -gt $count); elements=$items } |
            ConvertTo-Json -Depth 6 | Set-Content (Join-Path $diagnosticDirectory 'ui-failure-tree.json') -Encoding UTF8
        $report.diagnostics = 'ui-failure-tree.json (isolated test-process names/types only; no control values)'
        Write-Host "Process-owned UI diagnostics saved: $diagnosticDirectory\ui-failure-tree.json"
        if ($CaptureScreenshots -and $windows.Count -gt 0) {
            # A modal is usually the final descendant window. The screenshot
            # helper verifies process ownership, foreground state, and occlusion.
            Save-AppScreenshot $windows[-1] 'failure'
        }
    } catch {
        $report.diagnosticWarning = 'Could not collect process-owned UI diagnostics: ' + $_.Exception.Message
        Write-Host $report.diagnosticWarning
    }
}
function Start-TestApp {
    $script:process = Start-Process -FilePath $Executable -ArgumentList @('--ui-test-data', ('"' + $testData + '"')) -PassThru
    Wait-Window 'SessionDock'
}
function Stop-TestApp($Window) {
    Invoke-Button $Window 'Exit SessionDock'
    if (-not $script:process.WaitForExit(10000)) { throw 'Application did not exit after Exit SessionDock.' }
}
try {
    $main = Start-TestApp
    Invoke-Button $main 'Add PC'
    $editor = Wait-Window 'Add PC'
    Set-Field $editor 'PC name (DNS, IPv4, [IPv6], optionally :port)' 'cancelled.invalid'
    Invoke-Button $editor 'Cancel'
    $main = Wait-Window 'SessionDock'
    if (-not (Find-Element $main 'Add your first PC' ([System.Windows.Automation.ControlType]::Text))) { throw 'Cancel did not preserve the empty library.' }
    $report.tests += @{name='Cancel discards PC draft';result='Passed'}
    Invoke-Button $main 'Add PC'
    $editor = Wait-Window 'Add PC'
    Set-Field $editor 'PC name (DNS, IPv4, [IPv6], optionally :port)' 'offline-test.invalid:3390'
    Set-Field $editor 'Friendly name' 'UI automation test PC'
    Invoke-Button $editor 'Save'
    $main = Wait-Window 'SessionDock'
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $cardName = Find-Element $main 'UI automation test PC' ([System.Windows.Automation.ControlType]::Text)
        if ($cardName) { break }; Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $cardName) { throw 'Saved PC did not appear.' }
    $report.tests += @{name='Save PC without connecting';result='Passed'}
    Stop-TestApp $main
    $main = Start-TestApp
    if (-not (Find-Element $main 'UI automation test PC' ([System.Windows.Automation.ControlType]::Text))) { throw 'Saved profile did not survive restart.' }
    $report.tests += @{name='Profile persists after restart';result='Passed'}
    Save-AppScreenshot $main 'library'
    Invoke-Button $main 'Settings'
    $settings = Wait-Window 'SessionDock Settings'
    $general = Find-Element $settings 'General' ([System.Windows.Automation.ControlType]::ListItem)
    if ($general) { $general.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
    Save-AppScreenshot $settings 'settings'
    foreach ($category in @('General','Personalization','Full Screen Mode','Notifications','Keyboard','Resolutions','Credentials','Gateways','Groups','About')) {
        Select-SettingsPage $settings $category
    }
    $expectedVersion = ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path (Split-Path $Executable -Parent) 'SessionDock.dll')).ProductVersion -split '\+')[0]
    $aboutVersion = Find-Element $settings ("SessionDock " + $expectedVersion) ([System.Windows.Automation.ControlType]::Text)
    if (-not $aboutVersion) { throw 'About does not show the running app version.' }
    $report.tests += @{name='About shows the actual app version';result='Passed'}
    Save-AppScreenshot $settings 'about'
    Select-SettingsPage $settings 'Personalization'
    $previewToggle = Find-Element $settings 'Save last-session previews' ([System.Windows.Automation.ControlType]::CheckBox)
    if (-not $previewToggle) { throw 'Preview opt-in setting is missing.' }
    $toggle = $previewToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) { throw 'Preview capture must default to off.' }
    $toggle.Toggle()
    Invoke-Button $settings 'Done'
    $report.tests += @{name='Ten settings categories accessible';result='Passed'}
    Stop-TestApp (Wait-Window 'SessionDock')
    $main = Start-TestApp
    Invoke-Button $main 'Settings'
    $settings = Wait-Window 'SessionDock Settings'
    Select-SettingsPage $settings 'Personalization'
    $previewToggle = Find-Element $settings 'Save last-session previews' ([System.Windows.Automation.ControlType]::CheckBox)
    if ($previewToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) { throw 'Preview opt-in did not survive restart.' }
    Invoke-Button $settings 'Clear previews'
    $notice = Wait-Window 'Previews cleared'
    Invoke-Button $notice 'OK'
    $settings = Wait-Window 'SessionDock Settings'
    $previewToggle = Find-Element $settings 'Save last-session previews' ([System.Windows.Automation.ControlType]::CheckBox)
    if ($previewToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) { throw 'Clear previews did not disable capture.' }
    Save-AppScreenshot $settings 'settings'
    Invoke-Button $settings 'Done'
    $report.tests += @{name='Preview defaults off, opt-in persists, clear disables capture';result='Passed'}
    Stop-TestApp (Wait-Window 'SessionDock')
    $report.status='Passed'
    'UI smoke checks passed. No remote connection was attempted. DPI, Narrator, remote keys and installation still require manual tests.'
} catch {
    $originalFailure = $_
    $report.status='Failed'; $report.failure=$_.Exception.Message
    Save-UiFailureDiagnostics
    throw $originalFailure
} finally {
    # Only the process started for this isolated test is terminated on a failed check.
    if ($script:process -and -not $script:process.HasExited) { Stop-Process -Id $script:process.Id }
    $out = Join-Path $root 'artifacts\verification'; New-Item $out -ItemType Directory -Force | Out-Null
    $report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'ui-smoke.json') -Encoding UTF8
    Write-Host "Test metadata retained at $testData; report at $out\ui-smoke.json."
}
