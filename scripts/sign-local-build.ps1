param(
    [Parameter(Mandatory = $true)]
    [string[]]$FilePaths
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$certSubject = "CN=KikisenApp Local Code Signing"
$tempCertPath = Join-Path $env:TEMP "kikisenapp-local-signing.cer"

$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $certSubject -and
        $_.EnhancedKeyUsageList.ObjectId -contains "1.3.6.1.5.5.7.3.3"
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Subject $certSubject `
        -Type CodeSigningCert `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm sha256 `
        -NotAfter (Get-Date).AddYears(5)
}

Export-Certificate -Cert $certificate -FilePath $tempCertPath -Force | Out-Null

$trustedRoot = Get-ChildItem Cert:\CurrentUser\Root | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } | Select-Object -First 1
if (-not $trustedRoot) {
    Import-Certificate -FilePath $tempCertPath -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
}

$trustedPublisher = Get-ChildItem Cert:\CurrentUser\TrustedPublisher | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } | Select-Object -First 1
if (-not $trustedPublisher) {
    Import-Certificate -FilePath $tempCertPath -CertStoreLocation Cert:\CurrentUser\TrustedPublisher | Out-Null
}

foreach ($filePath in $FilePaths) {
    if (-not (Test-Path $filePath)) {
        throw "ファイルが見つかりません: $filePath"
    }

    Unblock-File -Path $filePath -ErrorAction SilentlyContinue

    $result = Set-AuthenticodeSignature -FilePath $filePath -Certificate $certificate -HashAlgorithm sha256
    if ($result.Status -ne "Valid") {
        throw "署名に失敗しました: $filePath / $($result.StatusMessage)"
    }
}

Remove-Item $tempCertPath -ErrorAction SilentlyContinue
