[CmdletBinding()]
param(
    [string]$Version,
    [string]$FileVersion,
    [string]$SigningCertificateThumbprint,
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$CertificateStore = "CurrentUser",
    [string]$TimestampUrl = "http://timestamp.acs.microsoft.com"
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$propsPath = Join-Path $root "Directory.Build.props"
[xml]$props = Get-Content -LiteralPath $propsPath -Raw
if (-not $Version) {
    $Version = [string]$props.Project.PropertyGroup.Version
}
if (-not $FileVersion) {
    $FileVersion = [string]$props.Project.PropertyGroup.FileVersion
}
if ($Version -notmatch '^[0-9A-Za-z][0-9A-Za-z.-]+$') {
    throw "Version contains unsupported installer filename characters: $Version"
}
if ($FileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "FileVersion must contain four numeric components: $FileVersion"
}

$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $root "artifacts\installer\$Version"))
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $root "artifacts\installer"))
if (-not $artifactRoot.StartsWith($allowedRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Installer staging path escaped the expected artifact directory."
}
$publish = Join-Path $artifactRoot "publish"
$output = Join-Path $artifactRoot "output"
if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publish, $output -Force | Out-Null

dotnet restore (Join-Path $root "src\PwaDrop.App\PwaDrop.App.csproj") `
    --runtime win-x64 `
    -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

dotnet publish (Join-Path $root "src\PwaDrop.App\PwaDrop.App.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publish `
    --no-restore `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $root "LICENSE") -Destination (Join-Path $publish "LICENSE.txt")
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination (Join-Path $publish "README.md")
$unexpectedSymbols = @(Get-ChildItem -LiteralPath $publish -Recurse -Filter *.pdb -File)
if ($unexpectedSymbols.Count -ne 0) {
    throw "The user installer staging directory contains debug symbols."
}

$isccCandidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 7\ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 7\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
$iscc = $isccCandidates | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup Compiler was not found. Install JRSoftware.InnoSetup with winget."
}

$isccArguments = @(
    "/DSourceDir=$publish",
    "/DOutputDir=$output",
    "/DRepoRoot=$root",
    "/DAppVersion=$Version",
    "/DFileVersion=$FileVersion"
)

if ($SigningCertificateThumbprint) {
    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    $signTool = Get-ChildItem -LiteralPath $kitsRoot -Filter signtool.exe -Recurse -File |
        Where-Object FullName -Match '\\x64\\signtool\.exe$' |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $signTool) {
        throw "signtool.exe was not found. Install the Windows 11 SDK."
    }
    $storeArgument = if ($CertificateStore -eq "LocalMachine") { "/sm" } else { $null }
    $signTargets = @(
        (Join-Path $publish "PwaDrop.exe"),
        (Get-ChildItem -LiteralPath (Join-Path $publish "Hook") -Recurse -Filter *.exe -File | Select-Object -ExpandProperty FullName),
        (Get-ChildItem -LiteralPath (Join-Path $publish "Hook") -Recurse -Filter *.dll -File | Select-Object -ExpandProperty FullName)
    ) | Where-Object { $_ }
    foreach ($target in $signTargets) {
        $arguments = @("sign", "/sha1", $SigningCertificateThumbprint, "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256")
        if ($storeArgument) {
            $arguments += $storeArgument
        }
        $arguments += $target
        & $signTool.FullName @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Signing failed for $target with exit code $LASTEXITCODE."
        }
    }
    $innoSignCommand = '$q{0}$q sign /sha1 {1} /fd SHA256 /tr $q{2}$q /td SHA256 {3} $f' -f `
        $signTool.FullName, $SigningCertificateThumbprint, $TimestampUrl, $storeArgument
    $isccArguments += "/Sproduction=$innoSignCommand"
    $isccArguments += "/DSignToolName=production"
}

$isccArguments += (Join-Path $root "installer\PwaDrop.iss")
& $iscc @isccArguments
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installer = Join-Path $output "PWADrop-Setup-v$Version-win-x64.exe"
if (-not (Test-Path -LiteralPath $installer)) {
    throw "The expected installer was not created: $installer"
}
if ($SigningCertificateThumbprint) {
    foreach ($target in @($signTargets + $installer)) {
        & $signTool.FullName verify /pa /all $target
        if ($LASTEXITCODE -ne 0) {
            throw "Signature verification failed for $target with exit code $LASTEXITCODE."
        }
    }
}
$hash = Get-FileHash -LiteralPath $installer -Algorithm SHA256
$checksumPath = Join-Path $output "SHA256SUMS.txt"
$checksumLine = '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $installer)
[System.IO.File]::WriteAllText($checksumPath, $checksumLine + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

Write-Host "Created $installer"
Write-Host $checksumLine
