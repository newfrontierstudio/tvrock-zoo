[Setup]
AppId={{632AF8C7-589A-433F-93CF-5AF286F6DFE6}
AppName=TVRock Zoo
AppVersion=0.1.0
AppPublisher=newfrontierstudio
AppPublisherURL=https://github.com/newfrontierstudio/tvrock-zoo
DefaultDirName={localappdata}\Programs\TVRockZoo
DefaultGroupName=TVRock Zoo
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts
OutputBaseFilename=TVRockZoo-0.1.0-Setup-x64
SetupIconFile=..\src\App\app.ico
UninstallDisplayIcon={app}\TVRockZoo.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
InfoBeforeFile=..\README.md

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成"; Flags: unchecked

[Files]
Source: "..\artifacts\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\TVRock Zoo"; Filename: "{app}\TVRockZoo.exe"
Name: "{autodesktop}\TVRock Zoo"; Filename: "{app}\TVRockZoo.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\TVRockZoo.exe"; Description: "TVRock Zooを起動（初回は接続設定）"; Flags: nowait postinstall skipifsilent
