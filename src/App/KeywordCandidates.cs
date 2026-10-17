using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    // A cached text search is advisory; TVRock remains authoritative for all reservation conditions.
    internal static bool KeywordTextCandidate(KeywordEntry key, Show show)
    {
        if (key.Stations.Length > 0 && !key.Stations.Split(" / ").Any(station=>Data.SameStation(station,show.Station))) return false;
        if (key.Days.Length > 0 && !key.Days.Contains("日月火水木金土"[(int)show.Start.DayOfWeek])) return false;
        if (!MatchesQuery(show,key.Title)) return false;
        return MatchesQuery(show with { Title=show.Description },key.Detail);
    }
    void UpdateKeywordCandidates()
    {
        var upcoming=_nativeShows.Where(s=>s.End>DateTimeOffset.Now).OrderBy(s=>s.Start).ToArray();
        foreach(DataGridViewRow row in _nativeKeywords.Rows)
        {
            if(row.Tag is not KeywordEntry key)continue;
            var cell=row.Cells["candidates"];
            if(_nativeShows.Count==0){cell.Value="番組表未取得";continue;}
            try {
                var hits=upcoming.Where(s=>KeywordTextCandidate(key,s)).ToArray();
                cell.Value=hits.Length==0?"取得分に候補なし":$"{hits.Length}件｜{hits[0].Start:MM/dd HH:mm} {hits[0].Title}";
                cell.ToolTipText="保存済み番組表のタイトル・詳細・局・曜日による参考候補です。時間・ジャンル・放送種別やTVRock固有の検索方式は未反映です。\n"+string.Join("\n",hits.Take(8).Select(s=>$"{s.Start:MM/dd HH:mm} {s.Station} {s.Title}"));
            } catch(Exception e) when(e is ArgumentException or System.Text.RegularExpressions.RegexMatchTimeoutException) {cell.Value="検索式を確認";cell.ToolTipText="この検索式の参考候補を計算できません。検索ワードで探す／条件を編集をご利用ください。";}
        }
    }
}
