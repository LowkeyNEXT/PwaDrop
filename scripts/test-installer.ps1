[CmdletBinding()]
param(
    [string]$InstallerPath,
    [switch]$SkipLaunch
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
[xml]$props = Get-Content -LiteralPath (Join-Path $root "Directory.Build.props") -Raw
$version = [string]$props.Project.PropertyGroup.Version
if (-not $InstallerPath) {
    $InstallerPath = Join-Path $root "artifacts\installer\$version\output\PWADrop-Setup-v$version-win-x64.exe"
}
$InstallerPath = [System.IO.Path]::GetFullPath($InstallerPath)
if (-not (Test-Path -LiteralPath $InstallerPath)) {
    throw "Installer not found: $InstallerPath"
}

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{C97D6EC5-BD37-4B82-B0EF-CC55E76EA141}_is1"
$existingRun = (Get-ItemProperty -LiteralPath $runKey -Name PWADrop -ErrorAction SilentlyContinue).PWADrop
if ($existingRun -or (Test-Path -LiteralPath $uninstallKey)) {
    throw "Installer smoke testing refuses to replace an existing per-user PWADrop installation."
}

$testRoot = [System.IO.Path]::GetFullPath((Join-Path $root "artifacts\installer-test"))
$testInstall = Join-Path $testRoot ([Guid]::NewGuid().ToString("N"))
if (-not $testInstall.StartsWith($testRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Installer test path escaped the artifact directory."
}
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$installLog = Join-Path $testRoot "install.log"
$appProcess = $null

try {
    $install = Start-Process -FilePath $InstallerPath `
        -ArgumentList "/CURRENTUSER", "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/TASKS=startup", "/DIR=$testInstall", "/LOG=$installLog" `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    if ($install.ExitCode -ne 0) {
        throw "Installer failed with exit code $($install.ExitCode)."
    }

    $runValue = (Get-ItemProperty -LiteralPath $runKey -Name PWADrop -ErrorAction Stop).PWADrop
    $expectedRun = '"' + (Join-Path $testInstall "PwaDrop.exe") + '" --startup'
    if (-not $runValue.Equals($expectedRun, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected startup registration: $runValue"
    }

    $upgrade = Start-Process -FilePath $InstallerPath `
        -ArgumentList "/CURRENTUSER", "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/DIR=$testInstall" `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    if ($upgrade.ExitCode -ne 0) {
        throw "Installer upgrade failed with exit code $($upgrade.ExitCode)."
    }
    $preservedRun = (Get-ItemProperty -LiteralPath $runKey -Name PWADrop -ErrorAction Stop).PWADrop
    if (-not $preservedRun.Equals($expectedRun, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The upgrade did not preserve the startup selection."
    }

    $disableStartup = Start-Process -FilePath $InstallerPath `
        -ArgumentList "/CURRENTUSER", "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/DIR=$testInstall", "/MERGETASKS=!startup" `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    if ($disableStartup.ExitCode -ne 0) {
        throw "Startup task update failed with exit code $($disableStartup.ExitCode)."
    }
    if ((Get-ItemProperty -LiteralPath $runKey -Name PWADrop -ErrorAction SilentlyContinue).PWADrop) {
        throw "The installer did not remove the deselected startup registration."
    }

    $enableStartup = Start-Process -FilePath $InstallerPath `
        -ArgumentList "/CURRENTUSER", "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/DIR=$testInstall", "/MERGETASKS=startup" `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    if ($enableStartup.ExitCode -ne 0) {
        throw "Startup task re-enable failed with exit code $($enableStartup.ExitCode)."
    }
    $restoredRun = (Get-ItemProperty -LiteralPath $runKey -Name PWADrop -ErrorAction Stop).PWADrop
    if (-not $restoredRun.Equals($expectedRun, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The installer did not restore the selected startup registration."
    }

    $generation = (Get-Content -LiteralPath (Join-Path $testInstall "Hook\current.txt") -Raw).Trim()
    $required = @(
        (Join-Path $testInstall "PwaDrop.exe"),
        (Join-Path $testInstall "LICENSE.txt"),
        (Join-Path $testInstall "README.md"),
        (Join-Path $testInstall "Hook\$generation\PwaDrop.Hook.dll"),
        (Join-Path $testInstall "Hook\$generation\PwaDrop.HookHost.exe"),
        (Join-Path $testInstall "unins000.exe")
    )
    foreach ($path in $required) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Installed file is missing: $path"
        }
    }
    if (Get-ChildItem -LiteralPath $testInstall -Recurse -Filter *.pdb -File) {
        throw "The installed payload contains debug symbols."
    }

    if ((Get-AuthenticodeSignature -LiteralPath $InstallerPath).Status -eq "Valid") {
        $signedFiles = @(
            (Join-Path $testInstall "PwaDrop.exe"),
            (Join-Path $testInstall "Hook\$generation\PwaDrop.Hook.dll"),
            (Join-Path $testInstall "Hook\$generation\PwaDrop.HookHost.exe"),
            (Join-Path $testInstall "unins000.exe")
        )
        foreach ($path in $signedFiles) {
            $signature = Get-AuthenticodeSignature -LiteralPath $path
            if ($signature.Status -ne "Valid") {
                throw "Installed signature validation failed for ${path}: $($signature.Status)"
            }
        }
    }

    if (-not $SkipLaunch) {
        $appProcess = Start-Process -FilePath (Join-Path $testInstall "PwaDrop.exe") `
            -ArgumentList "--startup" `
            -WindowStyle Hidden `
            -PassThru
        Start-Sleep -Seconds 2
        $appProcess.Refresh()
        if ($appProcess.HasExited) {
            throw "The installed app exited unexpectedly with code $($appProcess.ExitCode)."
        }
    }
}
finally {
    if ($appProcess -and -not $appProcess.HasExited) {
        Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
        Wait-Process -Id $appProcess.Id -ErrorAction SilentlyContinue
    }
    $uninstaller = Join-Path $testInstall "unins000.exe"
    if (Test-Path -LiteralPath $uninstaller) {
        $uninstall = Start-Process -FilePath $uninstaller `
            -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" `
            -WindowStyle Hidden `
            -Wait `
            -PassThru
        if ($uninstall.ExitCode -ne 0) {
            throw "Uninstaller failed with exit code $($uninstall.ExitCode)."
        }
    }
}

$remainingRun = (Get-ItemProperty -LiteralPath $runKey -Name PWADrop -ErrorAction SilentlyContinue).PWADrop
if ($remainingRun -or (Test-Path -LiteralPath $uninstallKey)) {
    throw "Installer registration remained after uninstall."
}
if (Test-Path -LiteralPath (Join-Path $testInstall "PwaDrop.exe")) {
    throw "Installed application files remained after uninstall."
}

$launchResult = if ($SkipLaunch) { "launch skipped" } else { "launch passed" }
Write-Host "Per-user install, startup selection, upgrade, $launchResult, and uninstall smoke test passed."
