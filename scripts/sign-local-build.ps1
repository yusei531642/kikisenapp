param(
    [Parameter(Mandatory = $true)]
    [string[]]$FilePaths
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$certSubject = "CN=yusei"
$tempCertPath = Join-Path $env:TEMP "kikisenapp-local-signing.cer"
$tempPfxPath = Join-Path $env:TEMP "kikisenapp-release-signing.pfx"

function Get-ImportedCertificateFromPfx {
    $pfxBase64 = $env:KIKISENAPP_SIGN_PFX_BASE64
    if ([string]::IsNullOrWhiteSpace($pfxBase64)) {
        return $null
    }

    $plainPassword = $env:KIKISENAPP_SIGN_PFX_PASSWORD
    if ($null -eq $plainPassword) {
        $plainPassword = ""
    }

    $password = ConvertTo-SecureString -String $plainPassword -AsPlainText -Force
    [System.IO.File]::WriteAllBytes($tempPfxPath, [Convert]::FromBase64String($pfxBase64))

    $imported = Import-PfxCertificate `
        -FilePath $tempPfxPath `
        -Password $password `
        -CertStoreLocation Cert:\CurrentUser\My `
        -Exportable

    if (-not $imported) {
        throw "Failed to import the signing certificate PFX."
    }

    return $imported
}

$certificate = Get-ImportedCertificateFromPfx

if (-not $certificate) {
    $certificate = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object {
            $_.Subject -eq $certSubject -and
            $_.EnhancedKeyUsageList.ObjectId -contains "1.3.6.1.5.5.7.3.3"
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Subject $certSubject `
        -Type CodeSigningCert `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm sha256 `
        -NotAfter (Get-Date).AddYears(10)
}

if ([string]::IsNullOrWhiteSpace($env:KIKISENAPP_SIGN_PFX_BASE64)) {
    Export-Certificate -Cert $certificate -FilePath $tempCertPath -Force | Out-Null

    $trustedRoot = Get-ChildItem Cert:\CurrentUser\Root | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } | Select-Object -First 1
    if (-not $trustedRoot) {
        Import-Certificate -FilePath $tempCertPath -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    }

    $trustedPublisher = Get-ChildItem Cert:\CurrentUser\TrustedPublisher | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } | Select-Object -First 1
    if (-not $trustedPublisher) {
        Import-Certificate -FilePath $tempCertPath -CertStoreLocation Cert:\CurrentUser\TrustedPublisher | Out-Null
    }
}

foreach ($filePath in $FilePaths) {
    if (-not (Test-Path $filePath)) {
        throw "File was not found: $filePath"
    }

    Unblock-File -Path $filePath -ErrorAction SilentlyContinue

    $result = Set-AuthenticodeSignature -FilePath $filePath -Certificate $certificate -HashAlgorithm sha256
    if ($result.Status -ne "Valid") {
        throw "Signing failed: $filePath / $($result.StatusMessage)"
    }
}

Remove-Item $tempCertPath -ErrorAction SilentlyContinue
Remove-Item $tempPfxPath -ErrorAction SilentlyContinue
