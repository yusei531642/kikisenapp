Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $PSScriptRoot "publish.ps1"
$signScript = Join-Path $PSScriptRoot "sign-local-build.ps1"
$iconScript = Join-Path $PSScriptRoot "ensure-app-icon.ps1"
$issPath = Join-Path $projectRoot "installer\kikisenapp.iss"
$installerDir = Join-Path $projectRoot "installer"
$downloadCacheDir = Join-Path $installerDir "downloads"
$generatedIncludePath = Join-Path $installerDir "download-files.generated.iss"
$distDir = Join-Path $projectRoot "dist"
$voicevoxLatestReleaseEndpoint = "https://api.github.com/repos/VOICEVOX/voicevox_engine/releases/latest"
$vbCablePageUrl = "https://vb-audio.com/Cable/"

if (-not (Test-Path $issPath)) {
    throw "Inno Setup script was not found: $issPath"
}

function Get-VoicevoxRelease {
    $headers = @{
        "User-Agent" = "KikisenAppInstaller/1.1"
        "Accept" = "application/vnd.github+json"
    }

    Invoke-RestMethod -Uri $voicevoxLatestReleaseEndpoint -Headers $headers
}

function Get-VoicevoxParts {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Release,

        [Parameter(Mandatory = $true)]
        [string]$Prefix
    )

    $firstPart = $Release.assets |
        Where-Object { $_.name -like "$Prefix*.7z.001" } |
        Select-Object -First 1

    if (-not $firstPart) {
        throw "VOICEVOX ENGINE asset was not found for prefix: $Prefix"
    }

    $archiveBaseName = $firstPart.name.Substring(0, $firstPart.name.Length - 4)
    $parts = @(
        $Release.assets |
            Where-Object {
                $_.name.StartsWith("$archiveBaseName.", [System.StringComparison]::OrdinalIgnoreCase) -and
                [System.Text.RegularExpressions.Regex]::IsMatch($_.name, '\.\d+$')
            } |
            Sort-Object name
    )

    if ($parts.Count -eq 0) {
        throw "VOICEVOX ENGINE archive parts were not found: $archiveBaseName"
    }

    [pscustomobject]@{
        Version = $Release.tag_name
        ArchiveBaseName = $archiveBaseName
        Parts = $parts
    }
}

function Get-VbCablePackageInfo {
    $response = Invoke-WebRequest -Uri $vbCablePageUrl -UseBasicParsing
    $match = [System.Text.RegularExpressions.Regex]::Match(
        $response.Content,
        'https://download\.vb-audio\.com/Download_CABLE/VBCABLE_Driver_Pack\d+\.zip',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if (-not $match.Success) {
        throw "VB-CABLE download URL was not found on the official page."
    }

    $downloadUrl = $match.Value
    [pscustomobject]@{
        DownloadUrl = $downloadUrl
        FileName = [System.IO.Path]::GetFileName(([uri]$downloadUrl).AbsolutePath)
    }
}

function Save-FileIfNeeded {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    if (Test-Path $DestinationPath) {
        $existingFile = Get-Item $DestinationPath
        if ($existingFile.Length -gt 0) {
            return
        }
    }

    Invoke-WebRequest -Uri $Url -OutFile $DestinationPath -UseBasicParsing
}

function Expand-VbCableArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    $expectedFile = Join-Path $DestinationPath "VBCABLE_Setup_x64.exe"
    if (Test-Path $expectedFile) {
        return
    }

    if (Test-Path $DestinationPath) {
        Remove-Item -Path $DestinationPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force $DestinationPath | Out-Null
    Expand-Archive -Path $ArchivePath -DestinationPath $DestinationPath -Force
}

function New-VoicevoxEntryLines {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VariantFolderName,

        [Parameter(Mandatory = $true)]
        [string]$CheckName,

        [Parameter(Mandatory = $true)]
        [object[]]$Parts
    )

    $lines = New-Object System.Collections.Generic.List[string]

    for ($index = 0; $index -lt $Parts.Count; $index++) {
        $part = $Parts[$index]
        $flags = "external download ignoreversion dontcopy"
        if ($index -eq 0) {
            $flags = "external download ignoreversion extractarchive"
        }

        $line = 'Source: "' + $part.browser_download_url +
            '"; DestDir: "{localappdata}\KikisenApp\voicevox-engine\' + $VariantFolderName +
            '"; DestName: "' + $part.name +
            '"; ExternalSize: ' + [string]$part.size +
            '; Flags: ' + $flags +
            '; Tasks: voicevox; Check: ' + $CheckName

        $lines.Add($line)
    }

    $lines.ToArray()
}

function Write-GeneratedInstallerInclude {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$VbCableExtractPath,

        [Parameter(Mandatory = $true)]
        [object]$NvidiaRelease,

        [Parameter(Mandatory = $true)]
        [object]$DirectMlRelease
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('; This file is generated by scripts/build-installer.ps1')
    $lines.Add('Source: "' + $VbCableExtractPath + '\*"; DestDir: "{tmp}\VBCABLE"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: vbcable')

    foreach ($line in (New-VoicevoxEntryLines -VariantFolderName ($NvidiaRelease.Version + "-nvidia") -CheckName "ShouldInstallVoicevoxNvidia" -Parts $NvidiaRelease.Parts)) {
        $lines.Add($line)
    }

    foreach ($line in (New-VoicevoxEntryLines -VariantFolderName ($DirectMlRelease.Version + "-directml") -CheckName "ShouldInstallVoicevoxDirectMl" -Parts $DirectMlRelease.Parts)) {
        $lines.Add($line)
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($Path, $lines, $utf8NoBom)
}

New-Item -ItemType Directory -Force $downloadCacheDir | Out-Null

& powershell -ExecutionPolicy Bypass -File $iconScript

$voicevoxRelease = Get-VoicevoxRelease
$nvidiaRelease = Get-VoicevoxParts -Release $voicevoxRelease -Prefix "voicevox_engine-windows-nvidia-"
$directMlRelease = Get-VoicevoxParts -Release $voicevoxRelease -Prefix "voicevox_engine-windows-directml-"
$vbCablePackage = Get-VbCablePackageInfo
$vbCableArchivePath = Join-Path $downloadCacheDir $vbCablePackage.FileName
$vbCableExtractPath = Join-Path $downloadCacheDir "VBCABLE"

Save-FileIfNeeded -Url $vbCablePackage.DownloadUrl -DestinationPath $vbCableArchivePath
Expand-VbCableArchive -ArchivePath $vbCableArchivePath -DestinationPath $vbCableExtractPath
Write-GeneratedInstallerInclude -Path $generatedIncludePath -VbCableExtractPath $vbCableExtractPath -NvidiaRelease $nvidiaRelease -DirectMlRelease $directMlRelease

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
    throw "ISCC.exe was not found. Inno Setup 6 is required."
}

New-Item -ItemType Directory -Force $distDir | Out-Null

& $isccPath $issPath

$setupExe = Join-Path $distDir "KikisenApp-Setup.exe"
if (-not (Test-Path $setupExe)) {
    throw "setup.exe was not created."
}

& powershell -ExecutionPolicy Bypass -File $signScript -FilePaths $setupExe
