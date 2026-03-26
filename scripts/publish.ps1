Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnetDir = Join-Path $projectRoot ".tools\dotnet"
$dotnetExe = Join-Path $dotnetDir "dotnet.exe"
$installScript = Join-Path $projectRoot ".tools\dotnet-install.ps1"
$iconScript = Join-Path $PSScriptRoot "ensure-app-icon.ps1"
$publishDir = Join-Path $projectRoot "publish\win-x64"

if (-not (Test-Path $dotnetExe)) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $installScript) | Out-Null
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
    & powershell -ExecutionPolicy Bypass -File $installScript -Version 8.0.412 -InstallDir $dotnetDir
}

New-Item -ItemType Directory -Force $publishDir | Out-Null

& powershell -ExecutionPolicy Bypass -File $iconScript

$env:PATH = "$dotnetDir;$env:PATH"
& $dotnetExe publish `
    (Join-Path $projectRoot "src\KikisenApp.Desktop\KikisenApp.Desktop.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir

$signScript = Join-Path $PSScriptRoot "sign-local-build.ps1"
$publishedExe = Join-Path $publishDir "KikisenApp.Desktop.exe"

& powershell -ExecutionPolicy Bypass -File $signScript -FilePaths $publishedExe
