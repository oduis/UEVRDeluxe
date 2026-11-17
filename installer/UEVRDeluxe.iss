; Inno Setup package for WoozyFace UEVRDeluxe v3.5.0.1
; Supply /DReleaseFilesDirectory=<absolute path to extracted Windows Release ZIP>.
#ifndef ReleaseFilesDirectory
  #error "Missing ReleaseFilesDirectory"
#endif

#define AppTitle "UEVR Easy Injector"
#define AppVersion "3.5.0.1"

[Setup]
AppId=WoozyFace.UEVRDeluxe.CustomGames
AppName={#AppTitle}
AppVersion={#AppVersion}
AppVerName={#AppTitle} {#AppVersion} - Custom Games
AppPublisher=WoozyFace
AppPublisherURL=https://github.com/WoozyFace/UEVRDeluxe
AppSupportURL=https://github.com/WoozyFace/UEVRDeluxe/issues
AppUpdatesURL=https://github.com/WoozyFace/UEVRDeluxe/releases
DefaultDirName={autopf}\UEVR Easy Injector - WoozyFace
DefaultGroupName=UEVR Easy Injector
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\UEVRDeluxe.exe
OutputBaseFilename=UEVRDeluxe-Setup-v3.5.0.1-x64
OutputDir=Output
SetupIconFile=..\UEVRDeluxe\Assets\uevr.ico
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#ReleaseFilesDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\UEVR Easy Injector"; Filename: "{app}\UEVRDeluxe.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\UEVR Easy Injector"; Filename: "{app}\UEVRDeluxe.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\UEVRDeluxe.exe"; Description: "{cm:LaunchProgram,UEVR Easy Injector}"; Flags: nowait postinstall skipifsilent runasoriginaluser
