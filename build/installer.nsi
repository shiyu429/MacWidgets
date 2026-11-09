; MacWidgets 安装程序（NSIS 3，可在 Linux 上用 makensis 构建）
; 按用户安装到 %LOCALAPPDATA%\Programs\MacWidgets，无需管理员权限。
Unicode true
!include "MUI2.nsh"

!ifndef VERSION
  !define VERSION "1.0.23"
!endif
!ifndef SRCDIR
  !define SRCDIR "..\..\dist\publish"
!endif
!ifndef OUTFILE
  !define OUTFILE "..\..\dist\MacWidgets-Setup-${VERSION}.exe"
!endif

Name "MacWidgets 桌面小组件"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\MacWidgets"
InstallDirRegKey HKCU "Software\MacWidgets" "InstallDir"
RequestExecutionLevel user
; 高分屏清晰：声明 DPI 感知，避免安装向导被系统模糊放大
ManifestDPIAware true
ManifestDPIAwareness "PerMonitorV2,System"
ManifestGdiScaling true
SetCompressor /SOLID lzma
BrandingText "MacWidgets ${VERSION}"
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "MacWidgets"
VIAddVersionKey "FileDescription" "MacWidgets 桌面小组件安装程序"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "MacWidgets"

!define MUI_ICON "..\src\MacWidgets.App\Assets\app.ico"
!define MUI_UNICON "..\src\MacWidgets.App\Assets\app.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\MacWidgets.exe"
!define MUI_FINISHPAGE_RUN_TEXT "立即运行 MacWidgets"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

Section "MacWidgets（必需）" SecMain
  SectionIn RO
  ; 结束正在运行的实例
  nsExec::Exec 'taskkill /IM MacWidgets.exe /F'
  Sleep 500
  SetOutPath "$INSTDIR"
  File /r "${SRCDIR}\*.*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\MacWidgets" "InstallDir" "$INSTDIR"
  !define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\MacWidgets"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "MacWidgets 桌面小组件"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "MacWidgets"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\MacWidgets.exe"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
  CreateDirectory "$SMPROGRAMS\MacWidgets"
  CreateShortcut "$SMPROGRAMS\MacWidgets\MacWidgets 桌面小组件.lnk" "$INSTDIR\MacWidgets.exe"
  CreateShortcut "$SMPROGRAMS\MacWidgets\卸载 MacWidgets.lnk" "$INSTDIR\Uninstall.exe"
SectionEnd

Section "桌面快捷方式" SecDesktop
  CreateShortcut "$DESKTOP\MacWidgets 桌面小组件.lnk" "$INSTDIR\MacWidgets.exe"
SectionEnd

Section /o "开机自动启动" SecAutostart
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MacWidgets" '"$INSTDIR\MacWidgets.exe" --autostart'
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /IM MacWidgets.exe /F'
  Sleep 500
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MacWidgets"
  Delete "$DESKTOP\MacWidgets 桌面小组件.lnk"
  RMDir /r "$SMPROGRAMS\MacWidgets"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MacWidgets"
  DeleteRegKey HKCU "Software\MacWidgets"
  MessageBox MB_YESNO|MB_ICONQUESTION "是否同时删除小组件布局、提醒事项、备忘录和照片等数据？$\r$\n（位于 $APPDATA\MacWidgets）" IDNO +2
    RMDir /r "$APPDATA\MacWidgets"
SectionEnd
