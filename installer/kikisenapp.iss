#define MyAppName "KikisenApp"
#define MyAppVersion "1.2.3"
#define MyAppPublisher "Yusei"
#define MyAppExeName "KikisenApp.Desktop.exe"

[Setup]
AppId={{7A63A3F7-4C1C-4664-8A59-4D6874579D21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
Compression=lzma
SolidCompression=yes
WizardStyle=modern dynamic
OutputDir=..\dist
OutputBaseFilename=KikisenApp-Setup
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\assets\kikisenapp.ico
ArchiveExtraction=full

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成"; GroupDescription: "追加アイコン:"; Flags: unchecked
Name: "vbcable"; Description: "VB-CABLE をインストール (インストーラーに同封)"; GroupDescription: "追加セットアップ:"; Flags: checkedonce

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#include "download-files.generated.iss"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\VBCABLE\VBCABLE_Setup_x64.exe"; Description: "VB-CABLE をインストール"; StatusMsg: "VB-CABLE をインストールしています..."; \
  Flags: waituntilterminated skipifsilent; Check: ShouldInstallVbCable and Is64BitInstallMode
Filename: "{tmp}\VBCABLE\VBCABLE_Setup.exe"; Description: "VB-CABLE をインストール"; StatusMsg: "VB-CABLE をインストールしています..."; \
  Flags: waituntilterminated skipifsilent; Check: ShouldInstallVbCable and not Is64BitInstallMode
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} を起動"; Flags: nowait postinstall skipifsilent

[Code]
function ShouldInstallVbCable: Boolean;
begin
  Result := WizardIsTaskSelected('vbcable');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if ShouldInstallVbCable then
    begin
      SuppressibleMsgBox(
        'VB-CABLE のセットアップが終わったら、必要に応じて Windows を再起動してください。',
        mbInformation,
        MB_OK,
        IDOK);
    end;
  end;
end;
