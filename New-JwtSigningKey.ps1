<#
.SYNOPSIS
Generates a durable JWT signing key for a web app and prints the production env vars.

.DESCRIPTION
Web apps must NOT use ephemeral signing keys outside Development.
This script generates a self-signed RSA certificate (PFX) and outputs:
- Auth__SigningKey__PfxBase64
- Auth__SigningKey__PfxPassword
- Auth__SigningKey__KeyId

The output is intended to be copied into your production secret store (Key Vault / ACA secrets / Kubernetes Secret).

.PARAMETER Subject
X.509 subject string used for the certificate.

.PARAMETER YearsValid
How many years the certificate should be valid.

.PARAMETER KeyLength
RSA key length. 3072 is a good default for long-lived internal signing.

.PARAMETER Password
PFX password. If omitted, a random password is generated and printed.

.PARAMETER KeyId
JWT 'kid' value. If omitted, defaults to the certificate thumbprint.

.PARAMETER OutPfxPath
Optional path to write the PFX to disk. If omitted, a temp file is used and deleted.

.PARAMETER KeepCertificateInStore
If set, the temporary certificate is not removed from the CurrentUser certificate store.

.EXAMPLE
pwsh .\New-JwtSigningKey.ps1

.EXAMPLE
powershell -ExecutionPolicy Bypass -File .\New-JwtSigningKey.ps1 -YearsValid 5 -OutPfxPath .\.secrets\azure-jwt.pfx
#>
param(
    [string]$Subject = "CN=agentrp-jwt",
    [ValidateRange(1, 30)]
    [int]$YearsValid = 5,
    [ValidateSet(2048, 3072, 4096)]
    [int]$KeyLength = 3072,
    [string]$Password,
    [string]$KeyId,
    [string]$OutPfxPath,
    [switch]$KeepCertificateInStore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function New-RandomPassword([int]$bytes = 32) {
    $data = New-Object byte[] $bytes
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    try { $rng.GetBytes($data) } finally { $rng.Dispose() }

    # Base64-url-ish to avoid characters that are annoying in some secret stores.
    $b64 = [Convert]::ToBase64String($data)
    return ($b64.TrimEnd("=").Replace("+", "-").Replace("/", "_"))
}

if ([string]::IsNullOrWhiteSpace($Password)) {
    $Password = New-RandomPassword
}

$notAfter = (Get-Date).AddYears($YearsValid)
$certStore = "Cert:\CurrentUser\My"

$cert = New-SelfSignedCertificate `
    -Subject $Subject `
    -CertStoreLocation $certStore `
    -KeyAlgorithm RSA `
    -KeyLength $KeyLength `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy Exportable `
    -NotAfter $notAfter

if ([string]::IsNullOrWhiteSpace($KeyId)) {
    $KeyId = $cert.Thumbprint
}

$securePassword = ConvertTo-SecureString $Password -AsPlainText -Force

$pfxPath = $OutPfxPath
$deletePfx = $false
if ([string]::IsNullOrWhiteSpace($pfxPath)) {
    $pfxPath = Join-Path $env:TEMP ("agentrp-jwt-{0}.pfx" -f ([Guid]::NewGuid().ToString("N")))
    $deletePfx = $true
} else {
    $dir = Split-Path -Parent $pfxPath
    if (-not [string]::IsNullOrWhiteSpace($dir) -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
}

try {
    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
    $pfxBytes = [IO.File]::ReadAllBytes($pfxPath)
    $pfxBase64 = [Convert]::ToBase64String($pfxBytes)
} finally {
    if ($deletePfx -and (Test-Path $pfxPath)) {
        Remove-Item -Force $pfxPath -ErrorAction SilentlyContinue
    }

    if (-not $KeepCertificateInStore) {
        $certPath = Join-Path $certStore $cert.Thumbprint
        Remove-Item -Force $certPath -ErrorAction SilentlyContinue
    }
}

Write-Output ""
Write-Output "Production JWT signing key (copy these into your secret store as env vars):"
Write-Output ""
Write-Output ("Auth__SigningKey__PfxBase64={0}" -f $pfxBase64)
Write-Output ("Auth__SigningKey__PfxPassword={0}" -f $Password)
Write-Output ("Auth__SigningKey__KeyId={0}" -f $KeyId)
Write-Output ""
Write-Output "Notes:"
Write-Output "- Do not commit these values to git."
Write-Output "- All production API instances must use the same values."
Write-Output "- If you rotate keys, existing tokens will stop validating unless you configure overlap validation."
