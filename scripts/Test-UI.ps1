#requires -Version 5.1
[CmdletBinding()]
param([string]$Executable = '')
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Windows with an interactive desktop is required.' }
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$root = Split-Path $PSScriptRoot -Parent
if (-not $Executable) { $Executable = Join-Path $root 'artifacts\SessionDock-win-x64\SessionDock.exe' }
if (-not (Test-Path $Executable)) { throw 'Build the application first or provide -Executable with the full path to SessionDock.exe.' }
$testData = Join-Path ([IO.Path]::GetTempPath()) ('SessionDock-UiTest-' + [Guid]::NewGuid().ToString('N'))
$script:process = $null
$report = [ordered]@{ schemaVersion=1; utc=[DateTime]::UtcNow.ToString('O'); os=[Environment]::OSVersion.Version.ToString(); tests=@(); realLogin='Not tested'; networkAccess=$false }
function Find-Element([System.Windows.Automation.AutomationElement]$Parent, [string]$Name, [System.Windows.Automation.ControlType]$Type) {
    $conditions = [System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name),
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $Type))
    $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $conditions)
}
function Wait-Window([string]$Title) {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $condition = [System.Windows.Automation.AndCondition]::new(
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$script:process.Id),
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Title))
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
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
    Invoke-Button $main 'Settings'
    $settings = Wait-Window 'SessionDock Settings'
    foreach ($category in @('General','Personalization','Full Screen Mode','Notifications','Keyboard','Resolutions','Credentials','Gateways','Groups')) {
        $item = Find-Element $settings $category ([System.Windows.Automation.ControlType]::ListItem)
        if (-not $item) { throw "Missing settings category '$category'." }
        $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    }
    Invoke-Button $settings 'Done'
    $report.tests += @{name='Nine settings categories accessible';result='Passed'}
    Stop-TestApp (Wait-Window 'SessionDock')
    $report.status='Passed'
    'UI smoke checks passed. No remote connection was attempted. DPI, Narrator, remote keys and installation still require manual tests.'
} catch {
    $report.status='Failed'; $report.failure=$_.Exception.Message
    throw
} finally {
    # Only the process started for this isolated test is terminated on a failed check.
    if ($script:process -and -not $script:process.HasExited) { Stop-Process -Id $script:process.Id }
    $out = Join-Path $root 'artifacts\verification'; New-Item $out -ItemType Directory -Force | Out-Null
    $report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'ui-smoke.json') -Encoding UTF8
    Write-Host "Test metadata retained at $testData; report at $out\ui-smoke.json."
}
