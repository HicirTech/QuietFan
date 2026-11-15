; QuietFan installer for Inno Setup 6. build.ps1 -Installer compiles it and passes the defines below.
#ifndef AppVersion
  #error Define AppVersion, AppExe, RepoDir, OutputDir and IconFile (build.ps1 -Installer does this)
#endif

#define AppName "QuietFan"
#define AppExeName "QuietFan.exe"
#define AppUrl "https://github.com/HicirTech/QuietFan"

[Setup]
AppId={{3F2B7C91-8E4D-4A6B-9C1F-5D7E2A4B8C60}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=HicirTech
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}.0
; Per-user install into %LOCALAPPDATA%\Programs: QuietFan needs no administrator rights.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
#ifdef ChineseIsl
Name: "zh"; MessagesFile: "{#ChineseIsl}"
#endif

[CustomMessages]
en.AutoStart=Start QuietFan when I sign in to Windows
en.Options=Options:
#ifdef ChineseIsl
zh.AutoStart=登录 Windows 时自动启动 QuietFan
zh.Options=选项:
#endif

[Tasks]
Name: "autostart"; Description: "{cm:AutoStart}"; GroupDescription: "{cm:Options}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RepoDir}\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Closes a running QuietFan, which restores the original CPU boost setting, and waits so its files can be removed.
Filename: "{app}\{#AppExeName}"; Parameters: "--restore"; RunOnceId: "RestoreBoost"; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\{#AppName}"

[Code]
// Close a running QuietFan, from any folder, before its files are replaced; it restores the boost setting on exit.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  ExtractTemporaryFile('{#AppExeName}');
  Exec(ExpandConstant('{tmp}\{#AppExeName}'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

// The app's own "start with Windows" item writes the same Run value; remove it even when the task was not chosen.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
end;
