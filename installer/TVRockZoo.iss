[Setup]
AppId={{632AF8C7-589A-433F-93CF-5AF286F6DFE6}
AppName=TVRock Zoo
AppVersion=1.0.6
AppPublisher=newfrontierstudio
AppPublisherURL=https://github.com/newfrontierstudio/tvrock-zoo
DefaultDirName={localappdata}\Programs\TVRockZoo
DefaultGroupName=TVRock Zoo
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts
OutputBaseFilename=TVRockZoo-1.0.6-Setup-x64
SetupIconFile=..\src\App\app.ico
UninstallDisplayIcon={app}\TVRockZoo.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
InfoBeforeFile=..\installer\はじめに.txt

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

[Code]
const
  WebViewKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  WebViewUrl = 'https://developer.microsoft.com/microsoft-edge/webview2/#download';
var
  RuntimePage: TWizardPage;
  RuntimeStatus: TNewStaticText;

function HasWebView2: Boolean;
var
  Version: String;
begin
  Result := (RegQueryStringValue(HKLM32, WebViewKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
  if not Result then
    Result := (RegQueryStringValue(HKCU32, WebViewKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

procedure RefreshRuntimeStatus;
begin
  if HasWebView2 then
    RuntimeStatus.Caption := 'WebView2はインストール済みです。そのまま「次へ」で進めます。'
  else
    RuntimeStatus.Caption := '詳細編集に必要なWebView2が見つかりません。下のボタンからMicrosoft公式サイトを開き、「Evergreen Bootstrapper」をダウンロードして実行してください。完了したら「再確認」を押します。予約一覧と番組表はWebView2なしでも利用できます。';
end;

procedure OpenRuntimeDownload(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec('open', WebViewUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure RecheckRuntime(Sender: TObject);
begin
  RefreshRuntimeStatus;
end;

procedure InitializeWizard;
var
  DownloadButton, RecheckButton: TNewButton;
  UrlLabel: TNewStaticText;
begin
  RuntimePage := CreateCustomPage(wpSelectDir, '必要なものを確認', '.NETは同梱済みです。追加で探したり、インストールする必要はありません。');
  RuntimeStatus := TNewStaticText.Create(RuntimePage);
  RuntimeStatus.Parent := RuntimePage.Surface;
  RuntimeStatus.SetBounds(0, 0, RuntimePage.SurfaceWidth, ScaleY(100));
  RuntimeStatus.AutoSize := False;
  RuntimeStatus.WordWrap := True;
  DownloadButton := TNewButton.Create(RuntimePage);
  DownloadButton.Parent := RuntimePage.Surface;
  DownloadButton.SetBounds(0, ScaleY(112), ScaleX(240), ScaleY(30));
  DownloadButton.Caption := 'Microsoft公式ダウンロードを開く';
  DownloadButton.OnClick := @OpenRuntimeDownload;
  RecheckButton := TNewButton.Create(RuntimePage);
  RecheckButton.Parent := RuntimePage.Surface;
  RecheckButton.SetBounds(ScaleX(250), ScaleY(112), ScaleX(90), ScaleY(30));
  RecheckButton.Caption := '再確認';
  RecheckButton.OnClick := @RecheckRuntime;
  UrlLabel := TNewStaticText.Create(RuntimePage);
  UrlLabel.Parent := RuntimePage.Surface;
  UrlLabel.SetBounds(0, ScaleY(155), RuntimePage.SurfaceWidth, ScaleY(60));
  UrlLabel.AutoSize := False;
  UrlLabel.WordWrap := True;
  UrlLabel.Caption := WebViewUrl;
  RefreshRuntimeStatus;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = RuntimePage.ID then RefreshRuntimeStatus;
end;

