#ifndef SourceDir
  #error SourceDir must point to the published PWADrop runtime.
#endif
#ifndef OutputDir
  #error OutputDir must point to the installer output directory.
#endif
#ifndef RepoRoot
  #error RepoRoot must point to the repository root.
#endif
#ifndef AppVersion
  #define AppVersion "0.1.0-alpha.7"
#endif
#ifndef FileVersion
  #define FileVersion "0.1.0.7"
#endif

[Setup]
AppId={{C97D6EC5-BD37-4B82-B0EF-CC55E76EA141}
AppName=PWADrop
AppVersion={#AppVersion}
AppVerName=PWADrop {#AppVersion}
AppPublisher=Riddle Next LLC
AppPublisherURL=https://github.com/LowkeyNEXT/PwaDrop
AppSupportURL=https://github.com/LowkeyNEXT/PwaDrop/issues
AppUpdatesURL=https://github.com/LowkeyNEXT/PwaDrop/releases
VersionInfoVersion={#FileVersion}
VersionInfoCompany=Riddle Next LLC
VersionInfoDescription=PWADrop installer
VersionInfoProductName=PWADrop
VersionInfoProductVersion={#FileVersion}
DefaultDirName={autopf}\PWADrop
DefaultGroupName=PWADrop
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22621
LicenseFile={#RepoRoot}\LICENSE
SetupIconFile={#RepoRoot}\src\PwaDrop.App\Assets\PwaDrop.ico
UninstallDisplayIcon={app}\PwaDrop.exe
OutputDir={#OutputDir}
OutputBaseFilename=PWADrop-Setup-v{#AppVersion}-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic
CloseApplications=yes
CloseApplicationsFilter=PwaDrop.exe
AppMutex=Local\PwaDrop.Singleton
RestartApplications=no
RestartIfNeededByRun=no
UsePreviousPrivileges=yes
UsePreviousTasks=yes
#ifdef SignToolName
SignTool={#SignToolName}
SignToolRetryCount=3
SignToolMinimumTimeBetween=1000
SignToolRunMinimized=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start PWADrop automatically at sign-in"; GroupDescription: "Startup:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\PWADrop"; Filename: "{app}\PwaDrop.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\PWADrop"; Filename: "{app}\PwaDrop.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PWADrop"; ValueData: """{app}\PwaDrop.exe"" --startup"; Flags: uninsdeletevalue; Tasks: startup
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "PWADrop"; Flags: deletevalue; Tasks: not startup
Root: HKA; Subkey: "Software\Riddle Next\PWADrop"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletevalue uninsdeletekeyifempty
Root: HKA; Subkey: "Software\Riddle Next\PWADrop"; ValueType: string; ValueName: "InstallScope"; ValueData: "{code:GetInstallScope}"; Flags: uninsdeletevalue uninsdeletekeyifempty

[Run]
Filename: "{app}\PwaDrop.exe"; Description: "Launch PWADrop"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent runasoriginaluser

[Code]
function GetInstallScope(Param: String): String;
begin
  if IsAdminInstallMode then
    Result := 'machine'
  else
    Result := 'user';
end;
