#define MyAppName    "Charmeleon"
#define MyAppSlug    "Charmeleon"
#define MyAppPublisher "University of Groningen"
#define MyAppURL     "https://github.com/markspan/Charmeleon"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppSlug}
DefaultGroupName={#MyAppSlug}
OutputDir=Output
OutputBaseFilename=CharmeleonSetup-{#MyAppVersion}
SetupIconFile=..\CharmeleonGUI\Resources\Charmeleon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "..\publish\Charmeleon\Charmeleon.exe";   DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\Charmeleon\eego-SDK.dll";  DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\Charmeleon\Resources\*";   DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Charmeleon";          Filename: "{app}\Charmeleon.exe"; IconFilename: "{app}\Resources\Charmeleon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Charmeleon";  Filename: "{app}\Charmeleon.exe"; IconFilename: "{app}\Resources\Charmeleon.ico"; Tasks: desktopicon

[Run]
; Register HTTP port 8765 so the built-in web view is reachable without elevation
Filename: "netsh.exe"; Parameters: "http add urlacl url=http://*:8765/ user=Everyone"; Flags: runhidden; StatusMsg: "Registering web view port..."
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""Charmeleon Web"" dir=in action=allow protocol=TCP localport=8765"; Flags: runhidden; StatusMsg: "Adding firewall rule..."

Filename: "{app}\Charmeleon.exe"; Description: "{cm:LaunchProgram,Charmeleon}"; Flags: nowait postinstall skipifsilent
