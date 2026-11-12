# Windows 上构建发布（PowerShell）：.\build\publish.ps1
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
$Version = if ($env:VERSION) { $env:VERSION } else { "1.0.26" }
$Dist = Join-Path (Resolve-Path "..") "dist"
if (Test-Path $Dist) { Remove-Item $Dist -Recurse -Force }
New-Item -ItemType Directory $Dist | Out-Null

dotnet test tests/MacWidgets.Core.Tests -c Release
dotnet publish src/MacWidgets.App/MacWidgets.App.csproj -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None -o "$Dist/publish"
Copy-Item "$Dist/publish/MacWidgets.exe" "$Dist/MacWidgets.exe"
Compress-Archive -Path "$Dist/publish/*" -DestinationPath "$Dist/MacWidgets-$Version-win-x64.zip"

dotnet publish src/MacWidgets.App/MacWidgets.App.csproj -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=false -p:DebugType=None -o "$Dist/publish-folder"
Compress-Archive -Path "$Dist/publish-folder/*" -DestinationPath "$Dist/MacWidgets-$Version-win-x64-folder.zip"

if (Get-Command makensis -ErrorAction SilentlyContinue) {
  Push-Location build; makensis -V2 "-DVERSION=$Version" installer.nsi; Pop-Location
}
Get-ChildItem $Dist
