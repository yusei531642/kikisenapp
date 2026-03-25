Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnetDir = Join-Path $projectRoot ".tools\dotnet"
$dotnetExe = Join-Path $dotnetDir "dotnet.exe"
$installScript = Join-Path $projectRoot ".tools\dotnet-install.ps1"

if (-not (Test-Path $dotnetExe)) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $installScript) | Out-Null
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
    & powershell -ExecutionPolicy Bypass -File $installScript -Version 8.0.412 -InstallDir $dotnetDir
}

$env:PATH = "$dotnetDir;$env:PATH"
& $dotnetExe run --project (Join-Path $projectRoot "src\KikisenApp.Desktop\KikisenApp.Desktop.csproj")
