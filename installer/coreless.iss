; ============================================================
;  CORELESS — Inno Setup installer script
;  Compiled by build-release.ps1 (or manually with ISCC.exe).
;  Version + source folder are passed in via /D defines:
;    ISCC.exe /DMyAppVersion=0.4.0 /DSourceDir=..\publish\win-x64 coreless.iss
; ============================================================

#define MyAppName "CORELESS"
#define MyAppPublisher "Louis Tetu"
#define MyAppURL "https://github.com/Louis765900/CORELESS"
#define MyAppExeName "CORELESS.exe"

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

; Folder holding the published self-contained build (default: x64)
#ifndef SourceDir
  #define SourceDir "..\publish\win-x64"
#endif

[Setup]
; Stable AppId — keep constant across versions so upgrades/uninstall work.
AppId={{9F3B7C4A-1E6D-4A2B-9C58-2F5E8C7D4A60}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
OutputDir=..\dist
OutputBaseFilename=CORELESS-Setup-v{#MyAppVersion}
SetupIconFile=..\src\CORELESS.App\coreless.ico
; Version info stamped on the Setup.exe (shown in file Properties > Details).
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCopyright=(C) 2025 {#MyAppPublisher}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Installer needs admin (writes to Program Files); the app itself also elevates.
PrivilegesRequired=admin
; x64 binary — allowed on x64 and on arm64 (via x64 emulation).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; Grab the whole published folder (single-file exe + any side files).
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
