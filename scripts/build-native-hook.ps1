[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "Visual Studio Build Tools were not found."
}

$visualStudio = & $vswhere `
    -products * `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -property installationPath |
    Select-Object -First 1
if (-not $visualStudio) {
    throw "The Visual C++ x64 build tools were not found."
}

$developerCommand = Join-Path $visualStudio "Common7\Tools\VsDevCmd.bat"
$source = Join-Path $root "native"
$hashInputs = @(
    (Join-Path $source "CMakeLists.txt"),
    (Join-Path $source "Hook.cpp"),
    (Join-Path $source "HookHost.cpp"),
    (Join-Path $source "NativeProbe.cpp")
)
$inputHashes = ($hashInputs | ForEach-Object { (Get-FileHash $_ -Algorithm SHA256).Hash }) -join ""
$fingerprintBytes = [Text.Encoding]::UTF8.GetBytes("$Configuration|$inputHashes")
$fingerprint = [Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData($fingerprintBytes)
).Substring(0, 16).ToLowerInvariant()
$generation = "$($Configuration.ToLowerInvariant())-$fingerprint"
$buildRoot = Join-Path $source "build-msvc-generations"
$build = Join-Path $buildRoot $generation
$command = @(
    "call `"$developerCommand`" -arch=x64 -host_arch=x64",
    "cmake -S `"$source`" -B `"$build`" -G `"NMake Makefiles`" -DCMAKE_BUILD_TYPE=$Configuration",
    "cmake --build `"$build`""
) -join " && "

& cmd.exe /d /s /c $command
if ($LASTEXITCODE -ne 0) {
    throw "Native hook build failed with exit code $LASTEXITCODE."
}

[IO.File]::WriteAllText(
    (Join-Path $source "build-msvc-current.txt"),
    $generation,
    [Text.UTF8Encoding]::new($false)
)
