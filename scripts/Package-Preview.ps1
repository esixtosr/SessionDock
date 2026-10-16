#requires -Version 5.1
[CmdletBinding()]
param([string]$Version = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props')
$sourceVersion = [string]$project.Project.PropertyGroup.Version
if (-not $Version) { $Version = $sourceVersion }
if ($Version -ne $sourceVersion -or $Version -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.]+)?$') { throw 'Package version must match Directory.Build.props.' }
$bundle = Join-Path $root 'artifacts/SessionDock-win-x64'
if (-not (Test-Path (Join-Path $bundle 'SessionDock.exe'))) { throw 'Publish the self-contained Windows app first.' }
$productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $bundle 'SessionDock.dll')).ProductVersion
$binaryVersion = ($productVersion -split '\+')[0]
if ($binaryVersion -ne $Version) { throw "Published app version $binaryVersion does not match package version $Version. Publish again before packaging." }
$commit = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source commit could not be read.' }
if (($productVersion -split '\+', 2)[1] -ne $commit) { throw 'Published build does not match the source commit. Publish again before packaging.' }
git diff --quiet HEAD
if ($LASTEXITCODE -ne 0) { throw 'Commit source changes before creating a numbered download.' }
$buildInfo = [ordered]@{
    version = $Version
    sourceCommit = $commit
    builtOn = [Environment]::OSVersion.VersionString
    generatedUtc = [DateTime]::UtcNow.ToString('O')
    signing = 'Unsigned testing preview'
    realRdpLoginVerified = $false
    reportLocation = 'GitHub Actions verification artifact for this source commit'
}
$buildInfo | ConvertTo-Json | Set-Content (Join-Path $bundle 'BUILD-INFO.json') -Encoding UTF8
Copy-Item scripts/Install-Local.ps1,scripts/Uninstall-Local.ps1,scripts/Install.cmd -Destination $bundle -Force
Copy-Item README.md,START-HERE.txt,THIRD-PARTY-NOTICES.md,CHANGELOG.md -Destination $bundle -Force
Copy-Item docs,licenses -Destination $bundle -Recurse -Force
$destination = Join-Path $root 'artifacts/release'
New-Item $destination -ItemType Directory -Force | Out-Null
$zip = Join-Path $destination "SessionDock-v$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath $zip -CompressionLevel Optimal
$source = Join-Path $destination "SessionDock-v$Version-source.zip"
git archive --format=zip --prefix=SessionDock/ --output=$source HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source archive could not be created.' }
$hashes = @($zip,$source) | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($_) }
[IO.File]::WriteAllText((Join-Path $destination 'SHA256SUMS.txt'), (($hashes -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
$buildInfo | ConvertTo-Json | Set-Content (Join-Path $destination 'BUILD-INFO.json') -Encoding UTF8
Write-Output "Prepared SessionDock $Version. The Windows ZIP contains the complete app: extract once and run Install.cmd or SessionDock.exe."
