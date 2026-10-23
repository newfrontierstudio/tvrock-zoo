using System.Text.Json;
namespace TsScheduler;
internal sealed partial class MainForm
{
    bool updateCheckBusy;DateTimeOffset nextUpdateCheck;LinkLabel? updateNotice;
    internal static bool IsNewStudioVersion(string tag,string current)
    {
        if(!Version.TryParse(tag.TrimStart('v','V'),out var available)||!Version.TryParse(current,out var installed))return false;
        return available>installed;
    }
    void InitializeUpdateNotifications(FlowLayoutPanel body)
    {
        var toggle=new CheckBox{Text="アップデートを知らせる",AutoSize=true,Checked=DisplayOptions.Current.UpdateNotifications};body.Controls.Add(toggle);
        body.Controls.Add(DetailNote("初期値はOFFです。ONのときだけ起動時と1日ごとにGitHubの公開版を確認します。自動インストールはしません。"));
        updateNotice=new LinkLabel{AutoSize=true,Visible=false,MaximumSize=new Size(740,0)};body.Controls.Add(updateNotice);
        updateNotice.LinkClicked+=(_,_)=>{try{if(updateNotice.Tag is string url)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,"更新ページを開けませんでした");}};
        toggle.CheckedChanged+=async(_,_)=>{bool old=DisplayOptions.Current.UpdateNotifications;try{DisplayOptions.Current.UpdateNotifications=toggle.Checked;DisplayOptions.Current.Save();if(!toggle.Checked)updateNotice.Visible=false;else{nextUpdateCheck=DateTimeOffset.MinValue;await CheckStudioUpdateAsync();}}catch(Exception e){DisplayOptions.Current.UpdateNotifications=old;MessageBox.Show(this,e.Message,"更新通知の設定を保存できません");}};
        Shown+=async(_,_)=>await CheckStudioUpdateAsync();_recommendTimer.Tick+=async(_,_)=>await CheckStudioUpdateAsync();
    }
    async Task CheckStudioUpdateAsync()
    {
        if(!DisplayOptions.Current.UpdateNotifications||updateCheckBusy||DateTimeOffset.Now<nextUpdateCheck)return;
        updateCheckBusy=true;nextUpdateCheck=DateTimeOffset.Now.AddDays(1);
        try
        {
            using var client=new System.Net.Http.HttpClient{Timeout=TimeSpan.FromSeconds(8)};client.DefaultRequestHeaders.UserAgent.ParseAdd("TVRockZoo/"+typeof(MainForm).Assembly.GetName().Version!.ToString(3));
            string response=await client.GetStringAsync("https://api.github.com/repos/newfrontierstudio/tvrock-zoo/releases/latest");using var json=JsonDocument.Parse(response);var release=json.RootElement;
            if(!release.TryGetProperty("tag_name",out var tagValue)||tagValue.ValueKind!=JsonValueKind.String||!release.TryGetProperty("html_url",out var urlValue)||urlValue.ValueKind!=JsonValueKind.String)return;
            string tag=tagValue.GetString()??"",url=urlValue.GetString()??"";
            if(IsDisposed||updateNotice==null||updateNotice.IsDisposed||!DisplayOptions.Current.UpdateNotifications)return;
            if(IsNewStudioVersion(tag,typeof(MainForm).Assembly.GetName().Version!.ToString(3))&&Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="github.com"&&uri.AbsolutePath.StartsWith("/newfrontierstudio/tvrock-zoo/releases/",StringComparison.Ordinal)){
                updateNotice.Text="新しいバージョン "+tag+" を公開しています（クリックで開く）";updateNotice.Tag=url;updateNotice.Visible=true;
            }
        }
        catch(System.Net.Http.HttpRequestException){}catch(TaskCanceledException){}catch(JsonException){}
        finally{updateCheckBusy=false;}
    }
}
