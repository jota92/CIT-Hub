$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "CITHub.Windows\CITHub.Windows.csproj"

dotnet publish $project -c Release -r win-x64 --self-contained true /p:Platform=x64
dotnet publish $project -c Release -r win-arm64 --self-contained true /p:Platform=ARM64

# Visual Studio's MSIX Packaging target combines both architecture packages.
msbuild $project /t:Publish /p:Configuration=Release /p:AppxBundle=Always /p:AppxBundlePlatforms="x64|ARM64" /p:UapAppxPackageBuildMode=StoreUpload

Write-Host "Created an MSIX Bundle containing x64 and ARM64 packages."
