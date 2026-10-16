#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$TlbImpPath,
    [string]$AxImpPath,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/interop')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not [Environment]::Is64BitProcess -or [Environment]::OSVersion.Platform -ne 'Win32NT') {
    throw 'Run this script in 64-bit Windows PowerShell 5.1 on the Windows test/build machine.'
}
function Resolve-SdkTool([string]$ExplicitPath, [string]$Name) {
    if ($ExplicitPath) { return (Resolve-Path -LiteralPath $ExplicitPath).Path }
    # Prefer the SDK's x64 tools: the generated RCW is explicitly x64 and AxImp
    # must be able to load it. Developer shells can put the x86 tools first on PATH.
    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs/Windows'
    if (Test-Path -LiteralPath $sdkRoot) {
        $matches = @(Get-ChildItem -LiteralPath $sdkRoot -Filter $Name -Recurse -File |
            Where-Object { $_.Directory.Name -eq 'x64' } | Sort-Object FullName -Descending)
        if ($matches.Count) { return $matches[0].FullName }
    }
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw "$Name was not found. Install the Visual Studio .NET desktop development workload and .NET Framework SDK tools, or supply the exact tool path."
}
$tlb = Resolve-SdkTool $TlbImpPath 'tlbimp.exe'
$ax = Resolve-SdkTool $AxImpPath 'aximp.exe'
$control = Join-Path $env:WINDIR 'System32/mstscax.dll'
if (-not (Test-Path -LiteralPath $control)) { throw 'The installed Windows MsTscAx control is missing.' }
$signature = Get-AuthenticodeSignature -LiteralPath $control
if ($signature.Status -ne 'Valid') { throw 'The installed control did not have a valid Windows Authenticode signature. No wrapper was generated.' }
$directory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$interop = Join-Path $directory 'Interop.MSTSCLib.dll'
$axinterop = Join-Path $directory 'AxInterop.MSTSCLib.dll'
Write-Output "Type library importer: $tlb"
Write-Output "ActiveX wrapper importer: $ax"
Push-Location -LiteralPath $directory
try {
    # AxImp also generates C# source; keep every generated file in the artifact directory.
    & $tlb $control "/out:$interop" /namespace:MSTSCLib /machine:X64 /silent
    if ($LASTEXITCODE -ne 0) { throw "tlbimp failed with exit code $LASTEXITCODE" }
    & $ax $control "/rcw:$interop" "/out:$axinterop" /source /silent
    if ($LASTEXITCODE -ne 0) { throw "aximp failed with exit code $LASTEXITCODE" }
} finally { Pop-Location }
$assembly = [Reflection.Assembly]::LoadFrom($interop)
$eventType = $assembly.GetType('MSTSCLib.IMsTscAxEvents', $true)
if ($eventType.GUID -ne [Guid]'336d5562-efa8-482e-8cb3-c5c0fc7a7db6') { throw 'Unexpected RDP event interface IID.' }
# Save exact local type-library metadata before validation so a failed CI run provides
# evidence for its ABI discrepancy; it contains type names only, never endpoints/secrets.
$eventMetadata = @($eventType.GetMethods() | Sort-Object Name | ForEach-Object {
    $method = $_
    $dispId = @($method.GetCustomAttributes([Runtime.InteropServices.DispIdAttribute], $false))
    [ordered]@{
        name = $method.Name
        dispatchId = if ($dispId.Count -eq 1) { $dispId[0].Value } else { $null }
        returnType = $method.ReturnType.FullName
        parameters = @($method.GetParameters() | ForEach-Object {
            [ordered]@{ name = $_.Name; type = $_.ParameterType.FullName; input = $_.IsIn; output = $_.IsOut }
        })
    }
})
$eventMetadata | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $directory 'interop-event-signatures.json') -Encoding UTF8
$nonScriptable = $assembly.GetType('MSTSCLib.IMsRdpClientNonScriptable3', $true)
if ($nonScriptable.GUID -ne [Guid]'b3378d90-0728-45c7-8ed7-b6159fb92219') { throw 'Unexpected non-scriptable interface IID.' }
$connectionBar = $nonScriptable.GetProperty('ConnectionBarText')
if (-not $connectionBar -or -not $connectionBar.CanWrite -or $connectionBar.PropertyType -ne [string]) {
    throw 'The generated non-scriptable interface cannot configure ConnectionBarText.'
}
# Microsoft documents UIParentWindowHandle on IMsRdpClientNonScriptable2.
$dialogType = $assembly.GetType('MSTSCLib.IMsRdpClientNonScriptable2', $true)
$dialogParent = $dialogType.GetProperty('UIParentWindowHandle')
if ($dialogType.GUID -ne [Guid]'17a5e535-4072-4fa4-af32-c8d0d47345e9' -or
    -not $dialogParent -or -not $dialogParent.CanWrite -or $dialogParent.PropertyType -ne [IntPtr]) {
    throw 'Unexpected native dialog parent interface or HWND signature.'
}
# Record the native setter slots from the same installed typelib for independent CI evidence.
# Runtime discovers/validates native signatures itself and never trusts offsets from JSON.
$passwordMetadata = @(
    @{ Type='MSTSCLib.IMsTscNonScriptable'; Iid='c1e6743a-41c1-4a74-832a-0dd06c1c7a0e'; Member='ClearTextPassword' },
    @{ Type='MSTSCLib.IMsRdpClientTransportSettings2'; Iid='67341688-d606-4c73-a5d2-2e0489009319'; Member='GatewayPassword' }
) | ForEach-Object {
    $entry = $_
    $type = $assembly.GetType($entry.Type, $true)
    if ($type.GUID -ne [Guid]$entry.Iid) { throw "Unexpected password interface IID: $($entry.Type)." }
    $property = $type.GetProperty($entry.Member)
    if (-not $property -or -not $property.CanWrite -or $property.PropertyType -ne [string]) {
        throw "The generated password interface is missing a string setter: $($entry.Member)."
    }
    $setter = $property.GetSetMethod()
    $parameters = @($setter.GetParameters())
    if ($setter.ReturnType -ne [void] -or $parameters.Count -ne 1 -or $parameters[0].ParameterType -ne [string]) {
        throw "Unexpected generated password setter signature: $($entry.Member)."
    }
    $marshal = @($parameters[0].GetCustomAttributes([Runtime.InteropServices.MarshalAsAttribute], $false))
    if ($marshal.Count -ne 1 -or $marshal[0].Value -ne [Runtime.InteropServices.UnmanagedType]::BStr) {
        throw "The generated password setter does not marshal an input BSTR: $($entry.Member)."
    }
    [ordered]@{
        interfaceName = $entry.Type
        interfaceId = $type.GUID.ToString()
        member = $entry.Member
        nativeSlot = [Runtime.InteropServices.Marshal]::GetComSlotForMethodInfo($setter)
        parameter = 'BSTR'
        nativeReturn = 'HRESULT'
    }
}
$passwordMetadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $directory 'interop-password-signatures.json') -Encoding UTF8
$expected = [ordered]@{
    OnConnecting=1; OnConnected=2; OnLoginComplete=3; OnDisconnected=4;
    OnEnterFullScreenMode=5; OnLeaveFullScreenMode=6; OnRequestGoFullScreen=8;
    OnRequestLeaveFullScreen=9; OnFatalError=10; OnWarning=11; OnRemoteDesktopSizeChange=12;
    OnIdleTimeoutNotification=13; OnRequestContainerMinimize=14; OnConfirmClose=15;
    OnReceivedTSPublicKey=16; OnAutoReconnecting=17; OnAuthenticationWarningDisplayed=18;
    OnAuthenticationWarningDismissed=19; OnLogonError=22; OnFocusReleased=23;
    OnAutoReconnected=33; OnAutoReconnecting2=34
}
foreach ($entry in $expected.GetEnumerator()) {
    $method = $eventType.GetMethod($entry.Key)
    if (-not $method) { throw "Installed event interface is missing $($entry.Key)." }
    $attribute = @($method.GetCustomAttributes([Runtime.InteropServices.DispIdAttribute], $false))
    if ($attribute.Count -ne 1 -or $attribute[0].Value -ne $entry.Value) { throw "Unexpected DISPID for $($entry.Key)." }
}
# Verify marshalled event signatures as well as DISPIDs. Enum outputs are represented by
# their declared enum type in the typelib; their underlying ABI must be Int32.
$signatures = @{
    OnConnecting=@(); OnConnected=@(); OnLoginComplete=@(); OnDisconnected=@('System.Int32');
    OnEnterFullScreenMode=@(); OnLeaveFullScreenMode=@(); OnRequestGoFullScreen=@();
    OnRequestLeaveFullScreen=@(); OnFatalError=@('System.Int32'); OnWarning=@('System.Int32');
    OnRemoteDesktopSizeChange=@('System.Int32','System.Int32'); OnIdleTimeoutNotification=@();
    OnRequestContainerMinimize=@(); OnConfirmClose=@('System.Boolean&');
    OnReceivedTSPublicKey=@('System.String','System.Boolean&');
    OnAutoReconnecting=@('System.Int32','System.Int32','System.Int32&');
    OnAuthenticationWarningDisplayed=@(); OnAuthenticationWarningDismissed=@();
    OnLogonError=@('System.Int32'); OnFocusReleased=@('System.Int32'); OnAutoReconnected=@();
    OnAutoReconnecting2=@('System.Int32','System.Boolean','System.Int32','System.Int32')
}
foreach ($entry in $signatures.GetEnumerator()) {
    $method = $eventType.GetMethod($entry.Key)
    if ($method.ReturnType -ne [void]) { throw "Unexpected event return ABI for $($entry.Key): expected System.Void, received $($method.ReturnType.FullName). Inspect interop-event-signatures.json." }
    $actual = @($method.GetParameters() | ForEach-Object {
        $type = $_.ParameterType
        $suffix = ''
        if ($type.IsByRef) { $type = $type.GetElementType(); $suffix = '&' }
        if ($type.IsEnum) { $type = [Enum]::GetUnderlyingType($type) }
        $type.FullName + $suffix
    })
    if (($actual -join ',') -ne ($entry.Value -join ',')) { throw "Unexpected event parameter ABI for $($entry.Key): expected [$($entry.Value -join ',')], received [$($actual -join ',')]. Inspect interop-event-signatures.json." }
}
$manifest = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    controlPath = $control
    controlFileVersion = (Get-Item -LiteralPath $control).VersionInfo.FileVersion
    controlSha256 = (Get-FileHash -LiteralPath $control -Algorithm SHA256).Hash
    tlbimp = $tlb
    aximp = $ax
    eventDispatchValidated = $true
    eventSignaturesValidated = $true
    nonScriptableConnectionBarValidated = $true
    authenticationDialogParentValidated = $true
    passwordSetterSignaturesValidated = $true
    realLoginValidated = $false
    outputHashes = @((Get-FileHash -LiteralPath $interop -Algorithm SHA256), (Get-FileHash -LiteralPath $axinterop -Algorithm SHA256)) | Select-Object Path, Hash
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $directory 'interop-manifest.json') -Encoding UTF8
Write-Output "Generated installed-control interop and validated event DISPIDs in $directory. This does not prove activation or an RDP logon."
