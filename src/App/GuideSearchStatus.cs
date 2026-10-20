using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    readonly TextBox _guideNgWords=new(){Width=210,PlaceholderText="NGワード（空白区切り）"};
    void InitializeGuideExclusions(FlowLayoutPanel searchbar)
    {
        searchbar.Controls.Add(new Label{Text="NGワード",AutoSize=true,Margin=new Padding(8,8,2,0)});searchbar.Controls.Add(_guideNgWords);
        _guideNgWords.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;RenderNativeGuide();}};
        new ToolTip().SetToolTip(_guideNgWords,"空白・改行で区切った語句のいずれかを番組名または詳細に含む番組を除外します。既存の予約は変更しません。");
    }
    internal static bool IsGuideExcluded(Show show,string words)
    {
        string text=Data.Clean(show.Title+"\n"+show.Description);
        return words.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Any(word=>text.Contains(Data.Clean(word),StringComparison.OrdinalIgnoreCase));
    }
    string GuideSearchSummary(int found,int excluded)
    {
        string query=string.IsNullOrWhiteSpace(_nativeQuery.Text)?"なし":_nativeQuery.Text;
        string ng=string.IsNullOrWhiteSpace(_guideNgWords.Text)?"なし":_guideNgWords.Text;
        return $"検索完了：{found}件 ／ NG除外 {excluded}件　検索語：{query} ／ NG：{ng}\n保存済み番組表 {_nativeShows.Count}件・取得 {_nativeGuideStamp}　（既存予約は変更しません）";
    }
}
