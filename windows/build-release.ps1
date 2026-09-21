param(
    [ValidateSet("x64", "ARM64")]
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "CITHub.Windows\CITHub.Windows.csproj"
$runtimeArchitecture = $Architecture.ToLowerInvariant()
dotnet publish $project -c Release -r "win-$runtimeArchitecture" --self-contained true `
    /p:Platform=$Architecture /p:AppxBundle=Never /p:GenerateAppxPackageOnBuild=true

Write-Host "Published Windows $Architecture package."
