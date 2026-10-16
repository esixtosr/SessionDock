#requires -Version 5.1
[CmdletBinding()]
param([string]$Version = '0.1.0-preview.1')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.]+)?$') { throw 'Use a version such as 0.1.0-preview.1.' }
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$bundle = Join-Path $root 'artifacts/SessionDock-win-x64'
if (-not (Test-Path (Join-Path $bundle 'SessionDock.exe'))) { throw 'Publish the self-contained Windows app first.' }
Copy-Item scripts/Install-Local.ps1,scripts/Uninstall-Local.ps1,scripts/Install.cmd -Destination $bundle -Force
Copy-Item README.md,START-HERE.txt,THIRD-PARTY-NOTICES.md,CHANGELOG.md -Destination $bundle -Force
Copy-Item docs,licenses -Destination $bundle -Recurse -Force
$destination = Join-Path $root 'artifacts/release'
New-Item $destination -ItemType Directory -Force | Out-Null
$zip = Join-Path $destination "SessionDock-v$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $bundle -DestinationPath $zip -CompressionLevel Optimal
$source = Join-Path $destination "SessionDock-v$Version-source.zip"
git archive --format=zip --prefix=SessionDock/ --output=$source HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source archive could not be created.' }
$hashes = @($zip,$source) | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($_) }
[IO.File]::WriteAllText((Join-Path $destination 'SHA256SUMS.txt'), (($hashes -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
$commit = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source commit could not be read.' }
[ordered]@{
    version = $Version
    sourceCommit = $commit
    builtOn = [Environment]::OSVersion.VersionString
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    signing = 'Unsigned testing preview'
    realRdpLoginVerified = $false
    reportLocation = 'GitHub Actions verification artifact for this source commit'
} | ConvertTo-Json | Set-Content (Join-Path $destination 'BUILD-INFO.json') -Encoding UTF8
Write-Output "Preview archives and checksums prepared in $destination. Real RDP login is still unverified."
