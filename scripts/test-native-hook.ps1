[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$generation = [IO.File]::ReadAllText(
    (Join-Path $root "native\build-msvc-current.txt")
).Trim()
$nativeOutput = Join-Path $root "native\build-msvc-generations\$generation\out"
$probePath = Join-Path $nativeOutput "PwaDrop.NativeProbe.exe"
$hookHostPath = Join-Path $nativeOutput "PwaDrop.HookHost.exe"
$hookPath = Join-Path $nativeOutput "PwaDrop.Hook.dll"

foreach ($required in @($probePath, $hookHostPath, $hookPath)) {
    if (-not (Test-Path $required)) {
        throw "Native hook test file is missing: $required"
    }
}

$probeProcess = Start-Process `
    -FilePath $probePath `
    -PassThru `
    -WindowStyle Hidden
$hookHostProcess = $null
try {
    Start-Sleep -Milliseconds 150
    $startTicks = $probeProcess.StartTime.ToUniversalTime().Ticks
    $hookHostProcess = Start-Process `
        -FilePath $hookHostPath `
        -ArgumentList @($probeProcess.Id, $startTicks, $hookPath) `
        -PassThru `
        -WindowStyle Hidden

    $deadline = [DateTime]::UtcNow.AddSeconds(7)
    while (
        [DateTime]::UtcNow -lt $deadline -and
        (-not $probeProcess.HasExited -or -not $hookHostProcess.HasExited)
    ) {
        Start-Sleep -Milliseconds 50
    }

    if (-not $hookHostProcess.HasExited) {
        throw "The native hook host exceeded its seven-second watchdog."
    }
    if (-not $probeProcess.HasExited) {
        throw "The native hook probe exceeded its seven-second watchdog."
    }
    if ($hookHostProcess.ExitCode -ne 0) {
        throw "The native hook host failed with exit code $($hookHostProcess.ExitCode)."
    }
    if ($probeProcess.ExitCode -ne 0) {
        throw "The native async-operation probe failed with exit code $($probeProcess.ExitCode)."
    }

    Write-Host "PWADrop native hook self-test passed."
}
finally {
    if ($hookHostProcess -and -not $hookHostProcess.HasExited) {
        $hookHostProcess.Kill($true)
    }
    if (-not $probeProcess.HasExited) {
        $probeProcess.Kill($true)
    }
    if ($hookHostProcess) {
        $hookHostProcess.Dispose()
    }
    $probeProcess.Dispose()
}

$hiddenNameProbe = Start-Process `
    -FilePath $probePath `
    -ArgumentList "--hide-ole-import-name" `
    -PassThru `
    -WindowStyle Hidden
$hiddenNameHost = $null
try {
    Start-Sleep -Milliseconds 150
    $hiddenNameTicks = $hiddenNameProbe.StartTime.ToUniversalTime().Ticks
    $hiddenNameHost = Start-Process `
        -FilePath $hookHostPath `
        -ArgumentList @($hiddenNameProbe.Id, $hiddenNameTicks, $hookPath) `
        -PassThru `
        -WindowStyle Hidden

    $deadline = [DateTime]::UtcNow.AddSeconds(7)
    while (
        [DateTime]::UtcNow -lt $deadline -and
        (-not $hiddenNameProbe.HasExited -or -not $hiddenNameHost.HasExited)
    ) {
        Start-Sleep -Milliseconds 50
    }

    if (-not $hiddenNameHost.HasExited -or -not $hiddenNameProbe.HasExited) {
        throw "The hidden-import-name hook test exceeded its seven-second watchdog."
    }
    if ($hiddenNameHost.ExitCode -ne 0) {
        throw "The hook could not recover a resolved DoDragDrop IAT slot after import names were hidden; host exit code $($hiddenNameHost.ExitCode)."
    }
    if ($hiddenNameProbe.ExitCode -ne 0) {
        throw "The hidden-import-name async-operation probe failed with exit code $($hiddenNameProbe.ExitCode)."
    }

    Write-Host "PWADrop hidden-import-name hook self-test passed."
}
finally {
    if ($hiddenNameHost -and -not $hiddenNameHost.HasExited) {
        $hiddenNameHost.Kill($true)
    }
    if (-not $hiddenNameProbe.HasExited) {
        $hiddenNameProbe.Kill($true)
    }
    if ($hiddenNameHost) {
        $hiddenNameHost.Dispose()
    }
    $hiddenNameProbe.Dispose()
}

$nonFileProbe = Start-Process `
    -FilePath $probePath `
    -ArgumentList "--no-cf-hdrop" `
    -PassThru `
    -WindowStyle Hidden
$nonFileHost = $null
try {
    Start-Sleep -Milliseconds 150
    $nonFileTicks = $nonFileProbe.StartTime.ToUniversalTime().Ticks
    $nonFileHost = Start-Process `
        -FilePath $hookHostPath `
        -ArgumentList @($nonFileProbe.Id, $nonFileTicks, $hookPath) `
        -PassThru `
        -WindowStyle Hidden

    $deadline = [DateTime]::UtcNow.AddSeconds(7)
    while (
        [DateTime]::UtcNow -lt $deadline -and
        (-not $nonFileProbe.HasExited -or -not $nonFileHost.HasExited)
    ) {
        Start-Sleep -Milliseconds 50
    }

    if (-not $nonFileHost.HasExited -or -not $nonFileProbe.HasExited) {
        throw "The non-file drag hook test exceeded its seven-second watchdog."
    }
    if ($nonFileHost.ExitCode -ne 0) {
        throw "The hook host failed for the non-file drag test with exit code $($nonFileHost.ExitCode)."
    }
    if ($nonFileProbe.ExitCode -ne 0) {
        throw "The hook incorrectly started async extraction for a drag without CF_HDROP; probe exit code $($nonFileProbe.ExitCode)."
    }

    Write-Host "PWADrop non-file drag bypass self-test passed."
}
finally {
    if ($nonFileHost -and -not $nonFileHost.HasExited) {
        $nonFileHost.Kill($true)
    }
    if (-not $nonFileProbe.HasExited) {
        $nonFileProbe.Kill($true)
    }
    if ($nonFileHost) {
        $nonFileHost.Dispose()
    }
    $nonFileProbe.Dispose()
}
