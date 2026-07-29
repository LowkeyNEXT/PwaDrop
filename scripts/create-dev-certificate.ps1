[CmdletBinding()]
param(
    [string]$OutputPath = "$PSScriptRoot\..\artifacts\PwaDrop-Development.pfx",
    [string]$Password = "pwadrop-dev"
)

$ErrorActionPreference = "Stop"
$certificate = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=PwaDrop Development" `
    -KeyUsage DigitalSignature `
    -FriendlyName "PwaDrop development package signing" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

$securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
New-Item (Split-Path -Parent $OutputPath) -ItemType Directory -Force | Out-Null
Export-PfxCertificate -Cert $certificate -FilePath $OutputPath -Password $securePassword | Out-Null

$cerPath = [System.IO.Path]::ChangeExtension($OutputPath, ".cer")
Export-Certificate -Cert $certificate -FilePath $cerPath | Out-Null
Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null

Write-Host "Created and trusted development certificate: $OutputPath"
Write-Warning "This certificate is for local development only. Never publish it or commit it to source control."

