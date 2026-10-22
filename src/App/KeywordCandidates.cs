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
    CancellationTokenSource? keywordCandidateCancellation;
    int keywordCandidateVersion;
    bool keywordCandidateDisposalHook;
    internal sealed record KeywordCandidateResult(KeywordEntry Key,string Value,string Tooltip);
    internal static KeywordCandidateResult[] CalculateKeywordCandidates(KeywordEntry[] keys,Show[] shows,DateTimeOffset now,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var upcoming=shows.Where(s=>s.End>now).OrderBy(s=>s.Start).ToArray();
        var results=new List<KeywordCandidateResult>();
        foreach(var key in keys)
        {
            cancellation.ThrowIfCancellationRequested();
            if(shows.Length==0){results.Add(new(key,"番組表未取得",""));continue;}
            try {
                var hits=new List<Show>();int count=0;
                foreach(var show in upcoming){cancellation.ThrowIfCancellationRequested();if(!KeywordTextCandidate(key,show))continue;count++;if(hits.Count<8)hits.Add(show);}
                string value=count==0?"取得分に候補なし":$"{count}件｜{hits[0].Start:MM/dd HH:mm} {hits[0].Title}";
                string tooltip="保存済み番組表のタイトル・詳細・局・曜日による参考候補です。時間・ジャンル・放送種別やTVRock固有の検索方式は未反映です。\n"+string.Join("\n",hits.Select(s=>$"{s.Start:MM/dd HH:mm} {s.Station} {s.Title}"));
                results.Add(new(key,value,tooltip));
            } catch(Exception e) when(e is ArgumentException or System.Text.RegularExpressions.RegexMatchTimeoutException) {results.Add(new(key,"検索式を確認","この検索式の参考候補を計算できません。検索ワードで探す／条件を編集をご利用ください。"));}
        }
        return results.ToArray();
    }
    void UpdateKeywordCandidates()=>_ = UpdateKeywordCandidatesAsync();
    async Task UpdateKeywordCandidatesAsync()
    {
        if(IsDisposed)return;
        if(!keywordCandidateDisposalHook){keywordCandidateDisposalHook=true;Disposed+=(_,_)=>keywordCandidateCancellation?.Cancel();}
        keywordCandidateCancellation?.Cancel();
        using var cancellation=new CancellationTokenSource();keywordCandidateCancellation=cancellation;
        int version=++keywordCandidateVersion;
        var rows=_nativeKeywords.Rows.Cast<DataGridViewRow>().Where(r=>r.Tag is KeywordEntry).ToArray();
        var keys=rows.Select(r=>(KeywordEntry)r.Tag!).ToArray();var shows=_nativeShows.ToArray();var now=DateTimeOffset.Now;
        foreach(var row in rows)row.Cells["candidates"].Value=shows.Length==0?"番組表未取得":"候補を検索中…";
        try
        {
            var results=await Task.Run(()=>CalculateKeywordCandidates(keys,shows,now,cancellation.Token),cancellation.Token);
            if(IsDisposed||version!=keywordCandidateVersion||cancellation.IsCancellationRequested)return;
            for(int i=0;i<results.Length;i++)
            {
                if(rows[i].DataGridView!=_nativeKeywords||!ReferenceEquals(rows[i].Tag,results[i].Key))continue;
                var cell=rows[i].Cells["candidates"];cell.Value=results[i].Value;cell.ToolTipText=results[i].Tooltip;
            }
        }
        catch(OperationCanceledException){}
        catch(Exception error){if(!IsDisposed&&version==keywordCandidateVersion)_nativeStatus.Text="候補を更新できません："+error.Message;}
        finally{if(ReferenceEquals(keywordCandidateCancellation,cancellation))keywordCandidateCancellation=null;}
    }
}
