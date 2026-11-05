; GoshehYar installer (Inno Setup 6). Build: ISCC /DAppVersion=1.0.0 installer\GoshehYar.iss
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{2F6B0427-2681-44FD-9A4C-60F66AEC9520}
AppName=GoshehYar
AppVersion={#AppVersion}
AppPublisher=GoshehYar
DefaultDirName={autopf}\GoshehYar
DefaultGroupName=GoshehYar
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=GoshehYar-Setup-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\GoshehYar.exe
AppMutex=GoshehYar.SingleInstance.v1
CloseApplications=yes
WizardStyle=modern
DisableDirPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\GoshehYar"; Filename: "{app}\GoshehYar.exe"
Name: "{group}\Uninstall GoshehYar"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GoshehYar"; Filename: "{app}\GoshehYar.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GoshehYar.exe"; Description: "{cm:LaunchProgram,GoshehYar}"; Flags: nowait postinstall skipifsilent

[Registry]
; Remove the autostart entry on uninstall (the app writes it when "Start with Windows" is on).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "GoshehYar"; Flags: dontcreatekey uninsdeletevalue

[Code]
// User data (%LOCALAPPDATA%\GoshehYar) is kept unless the user explicitly agrees to delete it.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\GoshehYar');
    if DirExists(DataDir) then
    begin
      if MsgBox('Also delete your settings and data (notes, backups, logs) in:' + #13#10 + DataDir + #13#10#13#10 +
                'Choose No to keep them.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
