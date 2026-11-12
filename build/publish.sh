#!/usr/bin/env bash
# 在 Linux/macOS 上交叉编译 Windows 版本（需要 .NET 10 SDK；安装包需要 makensis）。
set -euo pipefail
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
cd "$(dirname "$0")/.."
VERSION="${VERSION:-1.0.25}"
DIST="$(cd .. && pwd)/dist"
rm -rf "$DIST"
mkdir -p "$DIST"

dotnet test tests/MacWidgets.Core.Tests -c Release

# 1) 自包含单文件 exe
dotnet publish src/MacWidgets.App/MacWidgets.App.csproj -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -p:DebugType=None -o "$DIST/publish"
cp "$DIST/publish/MacWidgets.exe" "$DIST/MacWidgets.exe"
(cd "$DIST/publish" && python3 -m zipfile -c "$DIST/MacWidgets-$VERSION-win-x64.zip" MacWidgets.exe)

# 2) 自包含“文件夹版”（非单文件，作为单文件解包异常时的备用）
dotnet publish src/MacWidgets.App/MacWidgets.App.csproj -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=false -p:DebugType=None -o "$DIST/publish-folder"
(cd "$DIST/publish-folder" && python3 -m zipfile -c "$DIST/MacWidgets-$VERSION-win-x64-folder.zip" $(ls -A))

# 3) NSIS 安装包（可选）
if command -v makensis >/dev/null 2>&1; then
  (cd build && makensis -V2 -DVERSION="$VERSION" installer.nsi)
fi
ls -la "$DIST"
