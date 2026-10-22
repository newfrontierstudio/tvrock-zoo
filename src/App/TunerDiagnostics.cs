using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    async Task ShowTunerDiagnostics()
    {
        string text;
        try{
            var web=Data.TunerDevices(await NativeGet("reg"));var configured=Data.Tuners(Path.Combine(RockFolder,"dtv.ini"),web).Tuners;
            text=$"接続先：{ConnectionProfiles.Current.Active.Name}\r\nTVRockフォルダー：{RockFolder}\r\n\r\nWeb側のチューナー：{web.Count}系統\r\n"+string.Join("\r\n",web.OrderBy(p=>p.Key).Select(p=>$"チューナー{p.Key} → 予約先ID {p.Value}"))+$"\r\n\r\ndtv.iniと照合できた設定：{configured.Count}系統\r\n\r\n録画アプリ（TSTask・TVTest等）と局の設定は、TVRock本体の「設定」で確認してください。Zooには録画アプリを設定し直す必要はありません。\r\nWebのURLとフォルダーが同じTVRockを指しているかも確認してください。\r\nこの診断では予約を変更しません。";
        }catch(Exception error){text="チューナーの診断を完了できませんでした。\r\n"+error.Message;}
        using var dialog=new Form{Text="接続・チューナーの診断",ClientSize=new Size(650,500),Font=Font,StartPosition=FormStartPosition.CenterParent};
        var report=new TextBox{Text=text,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};dialog.Controls.Add(report);dialog.ShowDialog(this);
    }
}
