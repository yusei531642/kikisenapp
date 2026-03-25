Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $PSScriptRoot "publish.ps1"
$signScript = Join-Path $PSScriptRoot "sign-local-build.ps1"
$issPath = Join-Path $projectRoot "installer\kikisenapp.iss"
$distDir = Join-Path $projectRoot "dist"

if (-not (Test-Path $issPath)) {
    throw "Inno Setup 用の設定ファイルが見つかりません: $issPath"
}

& powershell -ExecutionPolicy Bypass -File $publishScript

$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$isccPath = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $isccPath) {
    $innoInstall = Get-ItemProperty `
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" `
        ,"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" `
        ,"HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like "Inno Setup*" } |
        Select-Object -First 1

    if ($innoInstall -and $innoInstall.InstallLocation) {
        $candidate = Join-Path $innoInstall.InstallLocation "ISCC.exe"
        if (Test-Path $candidate) {
            $isccPath = $candidate
        }
    }
}

if (-not $isccPath) {
    throw "ISCC.exe が見つかりません。Inno Setup 6 が必要です。"
}

New-Item -ItemType Directory -Force $distDir | Out-Null

& $isccPath $issPath

$setupExe = Join-Path $distDir "KikisenApp-Setup.exe"
if (-not (Test-Path $setupExe)) {
    throw "setup.exe の生成に失敗しました。"
}

& powershell -ExecutionPolicy Bypass -File $signScript -FilePaths $setupExe
