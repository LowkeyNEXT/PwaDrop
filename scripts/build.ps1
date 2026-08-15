[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

dotnet restore "$root\PwaDrop.slnx"
dotnet build "$root\PwaDrop.slnx" --configuration $Configuration --no-restore
& "$PSScriptRoot\test-native-hook.ps1"
dotnet test "$root\tests\PwaDrop.Core.Tests\PwaDrop.Core.Tests.csproj" --configuration $Configuration --no-build

