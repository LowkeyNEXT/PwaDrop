[CmdletBinding()]
param(
    [string]$Version = "1.0.7.0",
    [string]$IdentityName = "RiddleNEXT.PWADrop",
    [string]$Publisher = "CN=8548EF9A-500E-4856-8CEB-6C3736E43012",
    [string]$PublisherDisplayName = "RiddleNEXT",
    [string]$CertificatePath,
    [string]$CertificatePassword
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$storeRoot = Join-Path $artifacts "store\$Version"
$publish = Join-Path $storeRoot "publish"
$stage = Join-Path $storeRoot "stage"
$uploadStage = Join-Path $storeRoot "upload"
$packageName = "PWADrop_$($Version)_x64.msix"
$uploadName = "PWADrop_$($Version)_x64.msixupload"
$package = Join-Path $storeRoot $packageName
$uploadPackage = Join-Path $storeRoot $uploadName
$uploadArchive = Join-Path $storeRoot "$uploadName.zip"

function Assert-StoreVersion {
    param([Parameter(Mandatory)][string]$Value)

    $parts = $Value.Split('.')
    if ($parts.Count -ne 4) {
        throw "MSIX version must contain four numeric parts (Major.Minor.Build.Revision)."
    }

    $numbers = foreach ($part in $parts) {
        $number = 0
        if (-not [int]::TryParse($part, [ref]$number) -or $number -lt 0 -or $number -gt 65535) {
            throw "Every MSIX version part must be an integer from 0 through 65535."
        }
        $number
    }

    if ($numbers[0] -eq 0) {
        throw "The Microsoft Store requires a nonzero MSIX major version."
    }
    if ($numbers[3] -ne 0) {
        throw "The fourth MSIX version part is reserved by the Microsoft Store and must be 0."
    }
}

function Remove-BuildDirectory {
    param([Parameter(Mandatory)][string]$Path)

    $resolvedArtifacts = [System.IO.Path]::GetFullPath($artifacts).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedArtifacts, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside the artifacts folder: $resolvedPath"
    }
    Remove-Item -LiteralPath $resolvedPath -Recurse -Force -ErrorAction SilentlyContinue
}

Assert-StoreVersion -Value $Version

foreach ($requiredValue in @{
    IdentityName = $IdentityName
    Publisher = $Publisher
    PublisherDisplayName = $PublisherDisplayName
}.GetEnumerator()) {
    if ([string]::IsNullOrWhiteSpace($requiredValue.Value) -or $requiredValue.Value.Contains('__')) {
        throw "$($requiredValue.Key) must contain the exact value assigned in Partner Center."
    }
}

Remove-BuildDirectory -Path $publish
Remove-BuildDirectory -Path $stage
Remove-BuildDirectory -Path $uploadStage
Remove-Item -LiteralPath $package, $uploadPackage, $uploadArchive -Force -ErrorAction SilentlyContinue
New-Item $publish, $stage, $uploadStage -ItemType Directory -Force | Out-Null

dotnet publish "$root\src\PwaDrop.App\PwaDrop.App.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publish `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item "$publish\*" $stage -Recurse
Copy-Item "$root\packaging\Assets" "$stage\Assets" -Recurse

$manifest = Get-Content "$root\packaging\AppxManifest.xml" -Raw
$manifest = $manifest.Replace("__VERSION__", $Version).Replace("__IDENTITY_NAME__", $IdentityName).Replace("__PUBLISHER__", $Publisher).Replace("__PUBLISHER_DISPLAY_NAME__", $PublisherDisplayName)
if ($manifest.Contains('__')) {
    throw "The generated manifest still contains an unresolved placeholder."
}
Set-Content (Join-Path $stage "AppxManifest.xml") $manifest -Encoding UTF8

$kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
$makeAppx = Get-ChildItem $kitsRoot -Filter makeappx.exe -Recurse |
    Where-Object FullName -Match '\\x64\\makeappx.exe$' |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $makeAppx) {
    throw "makeappx.exe was not found. Install the Windows 11 SDK; Visual Studio is not required."
}

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
        throw "signtool.exe was not found. Install the Windows 11 SDK; Visual Studio is not required."
    }

    & $signTool.FullName sign /fd SHA256 /f $CertificatePath /p $CertificatePassword $package
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed with exit code $LASTEXITCODE."
    }
}

# A .msixupload is a ZIP container. Symbols are optional and deliberately stay
# out of this public package; release symbols remain a separate private artifact.
Copy-Item -LiteralPath $package -Destination (Join-Path $uploadStage $packageName)
Compress-Archive -Path (Join-Path $uploadStage '*') -DestinationPath $uploadArchive -CompressionLevel Optimal
Move-Item -LiteralPath $uploadArchive -Destination $uploadPackage

$packageHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
$uploadHash = (Get-FileHash -LiteralPath $uploadPackage -Algorithm SHA256).Hash
Set-Content -LiteralPath "$package.sha256" -Value "$packageHash  $packageName" -Encoding ascii
Set-Content -LiteralPath "$uploadPackage.sha256" -Value "$uploadHash  $uploadName" -Encoding ascii

$metadata = [ordered]@{
    productName = "PWADrop"
    storeId = "9NLG3ZRF0MM3"
    identityName = $IdentityName
    publisher = $Publisher
    publisherDisplayName = $PublisherDisplayName
    packageFamilyName = "RiddleNEXT.PWADrop_jpwktxafth4c0"
    version = $Version
    architecture = "x64"
    package = $packageName
    uploadPackage = $uploadName
    packageSha256 = $packageHash
    uploadPackageSha256 = $uploadHash
    signed = [bool]$CertificatePath
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $storeRoot "store-package-metadata.json") -Encoding UTF8

Remove-BuildDirectory -Path $publish
Remove-BuildDirectory -Path $stage
Remove-BuildDirectory -Path $uploadStage

Write-Host "Created Store upload package: $uploadPackage"
Write-Host "Created raw MSIX package:    $package"
Write-Host "Store packages may be submitted unsigned; Microsoft signs them after ingestion."

