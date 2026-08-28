#define AppName "OpenDefinery"
#define AppExeName "OpenDefinery-DesktopApp.exe"
#define AppVersion "0.1.0"
#define AppPublisher "Triple Zero Labs, LLC"
#define AppUrl "https://opendefinery.com"
; Release output of the WPF app (exe + dependencies + config + assets\).
#define SrcDir "..\OpenDefinery-DesktopApp\bin\Release\net472"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppId={{C4A9E1F7-2B3D-4E86-9A0F-5D7C8B2E6134}
; Per-user install (no admin/UAC): {autopf} resolves to %LocalAppData%\Programs
; when PrivilegesRequired=lowest. {autoprograms}/{autodesktop} resolve per-user too.
PrivilegesRequired=lowest
DefaultDirName={autopf}\OpenDefinery
DefaultGroupName=OpenDefinery
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
SetupIconFile=..\OpenDefinery-DesktopApp\favicon.ico
OutputDir=..\dist
OutputBaseFilename=OpenDefinery Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
; Package the whole Release output (exe, dependency DLLs, .config, assets\),
; excluding debug/IntelliSense/VS-host artifacts.
Source: "{#SrcDir}\*"; DestDir: "{app}"; \
    Excludes: "*.pdb,*.xml,*.vshost.*,*.manifest"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
