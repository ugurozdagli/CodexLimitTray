param([switch]$SelfContained, [string]$Dotnet = 'dotnet', [string]$OutputDirectory = 'release')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $standalone = if ($SelfContained) { 'true' } else { 'false' }
    & $Dotnet publish .\CodexLimitTray.csproj -c Release -r win-x64 --self-contained $standalone -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }
    Write-Host "Ready: $OutputDirectory\CodexLimitTray.exe"
} finally { Pop-Location }
