#define MyAppName "SlicerLauncher"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "3DPrintKings"
#define MyAppURL "https://www.3dprintkings.ch"
#define MyAppExeName "SlicerLauncher.exe"

[Setup]
AppId={{B8AA23C1-6DB2-447C-993C-1D157BCE8617}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\SlicerLauncher
DefaultGroupName=SlicerLauncher
DisableProgramGroupPage=yes
OutputDir=..\dist\Installer
OutputBaseFilename=SlicerLauncher-Setup
SetupIconFile=..\SlicerLauncher\Assets\SlicerLauncher.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\dist\Portable\SlicerLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\SlicerLauncher"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\SlicerLauncher"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch SlicerLauncher"; Flags: nowait postinstall skipifsilent
