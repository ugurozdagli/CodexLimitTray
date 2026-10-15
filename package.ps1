param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$projectXml = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'CodexLimitTray.csproj') -Raw)
$version = [string]$projectXml.Project.PropertyGroup[0].Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid project version.' }
$name = "CodexLimitTray-v$version-win-x64"
$buildDirectory = Join-Path $projectRoot 'work\package-build'
$stagingDirectory = Join-Path $projectRoot ('work\package-stage-' + [Guid]::NewGuid().ToString('N'))
$distributionDirectory = Join-Path $projectRoot 'dist'
& (Join-Path $projectRoot 'build.ps1') -SelfContained -Dotnet $Dotnet -OutputDirectory $buildDirectory
New-Item -ItemType Directory -Force -Path $stagingDirectory, $distributionDirectory | Out-Null
# Explicit allowlist. Never package the working directory recursively.
$documents = @('README.md','LICENSE','THIRD-PARTY-NOTICES.txt','PRIVACY.md','SECURITY.md','CHANGELOG.md','docs\README.tr.md','docs\TROUBLESHOOTING.md','docs\VERIFYING-DOWNLOADS.md','docs\images\hero.svg','docs\images\usage-panel.png','docs\images\taskbar-simple.png','docs\images\taskbar-detailed.png')
Copy-Item -LiteralPath (Join-Path $buildDirectory 'CodexLimitTray.exe') -Destination (Join-Path $stagingDirectory 'CodexLimitTray.exe')
foreach ($document in $documents) {
    $target = Join-Path $stagingDirectory $document
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination $target
}
$zipPath = Join-Path $distributionDirectory "$name.zip"
Compress-Archive -Path (Join-Path $stagingDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force
$portablePath = Join-Path $distributionDirectory "$name.exe"
Copy-Item -LiteralPath (Join-Path $buildDirectory 'CodexLimitTray.exe') -Destination $portablePath -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $distributionDirectory 'THIRD-PARTY-NOTICES.txt') -Force
$manifest = foreach ($artifact in @($zipPath, $portablePath)) {
    $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($artifact))"
}
[System.IO.File]::WriteAllText((Join-Path $distributionDirectory 'SHA256SUMS.txt'), (($manifest -join "`n") + "`n"), [System.Text.UTF8Encoding]::new($false))
Write-Host "Package: $zipPath"
Write-Host "Portable EXE: $portablePath"
Write-Host 'Checksums: dist/SHA256SUMS.txt'
