param(
    [ValidateSet("x64", "ARM64")]
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "CITHub.Windows\CITHub.Windows.csproj"
$runtimeArchitecture = $Architecture.ToLowerInvariant()

# WinUI XAML is compiled by the Visual Studio MSBuild toolchain on Windows.
msbuild $project /restore /p:Configuration=Release /p:Platform=$Architecture `
    "/p:RuntimeIdentifier=win-$runtimeArchitecture" /p:WindowsAppSDKSelfContained=true `
    /p:AppxBundle=Never /p:GenerateAppxPackageOnBuild=true `
    "/bl:$PSScriptRoot\windows-build-$runtimeArchitecture.binlog"

Write-Host "Published Windows $Architecture package."
