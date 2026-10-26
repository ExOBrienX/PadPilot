; Instalador de PadPilot (Inno Setup 6: https://jrsoftware.org/isinfo.php)
; Compilar:  iscc installer\PadPilot.iss   (después de "dotnet publish ... -o publish\folder", ver README)

#define AppName "PadPilot"
#define AppVersion "1.0.1"
#define AppPublisher "ExO"
#define AppExe "PadPilot.exe"

[Setup]
AppId={{8B60818E-015A-4FDE-B3C9-B76216C99353}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\src\PadPilot.App\Assets\padpilot.ico
OutputDir=..\publish
OutputBaseFilename=PadPilot-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; Instalación por usuario: no pide administrador.
PrivilegesRequired=lowest
WizardStyle=modern

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\folder\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Quita el inicio automático si estaba activado.
Filename: "reg.exe"; Parameters: "delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v PadPilot /f"; Flags: runhidden; RunOnceId: "RemoveAutostart"
