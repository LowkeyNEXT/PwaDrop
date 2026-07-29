[CmdletBinding()]
param(
    [string]$Version = "0.1.0.0",
    [string]$Publisher = "CN=PwaDrop Development",
    [string]$CertificatePath,
    [string]$CertificatePassword
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish\win-x64"
$stage = Join-Path $artifacts "msix\stage"
$package = Join-Path $artifacts "msix\PwaDrop-$Version-x64.msix"

Remove-Item $publish, $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item $publish, $stage -ItemType Directory -Force | Out-Null

dotnet publish "$root\src\PwaDrop.App\PwaDrop.App.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publish `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

Copy-Item "$publish\*" $stage -Recurse
Copy-Item "$root\packaging\Assets" "$stage\Assets" -Recurse

$manifest = Get-Content "$root\packaging\AppxManifest.xml" -Raw
$manifest = $manifest.Replace("__VERSION__", $Version).Replace("__PUBLISHER__", $Publisher)
Set-Content (Join-Path $stage "AppxManifest.xml") $manifest -Encoding UTF8

$kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
$makeAppx = Get-ChildItem $kitsRoot -Filter makeappx.exe -Recurse |
    Where-Object FullName -Match '\\x64\\makeappx.exe$' |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $makeAppx) {
    throw "makeappx.exe was not found. Install the Windows 11 SDK."
}

New-Item (Split-Path -Parent $package) -ItemType Directory -Force | Out-Null
& $makeAppx.FullName pack /d $stage /p $package /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx failed with exit code $LASTEXITCODE."
}

if ($CertificatePath) {
    $signTool = Get-ChildItem $kitsRoot -Filter signtool.exe -Recurse |
        Where-Object FullName -Match '\\x64\\signtool.exe$' |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $signTool) {
        throw "signtool.exe was not found. Install the Windows 11 SDK."
    }

    & $signTool.FullName sign /fd SHA256 /f $CertificatePath /p $CertificatePassword $package
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Created $package"

