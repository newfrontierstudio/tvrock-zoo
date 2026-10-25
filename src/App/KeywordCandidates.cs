using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    // A cached text search is advisory; TVRock remains authoritative for all reservation conditions.
    internal static bool KeywordTextCandidate(KeywordEntry key, Show show,bool stationAlreadyMatched=false)
    {
        if(key.Fields?.TryGetValue("STUDIOMINPROGRAMSECONDS",out string? threshold)==true){if(!int.TryParse(threshold,out int seconds)||seconds is <0 or >599999)throw new InvalidDataException("番組の長さの設定を確認してください。");if(seconds>0&&(show.End-show.Start).TotalSeconds<=seconds)return false;}
        if (!stationAlreadyMatched && key.Stations.Length > 0 && !key.Stations.Split(" / ").Any(station=>Data.SameStation(station,show.Station))) return false;
        if (key.Days.Length > 0 && !key.Days.Contains("日月火水木金土"[(int)show.Start.DayOfWeek])) return false;
        if (!MatchesQuery(show,key.Title)) return false;
        return MatchesQuery(show with { Title=show.Description },key.Detail);
    }
    readonly KeywordCandidateCache keywordCandidateCache=new();
    internal sealed class KeywordCandidateCache
    {
        internal int Calculations{get;private set;}
        readonly object sync=new();object? guide;string signature="";DateTimeOffset until;KeywordCandidateResult[]? cached;
        internal KeywordCandidateResult[] Get(KeywordEntry[] keys,Show[] shows,object guideIdentity,DateTimeOffset now,CancellationToken cancellation)
        {
            string input=System.Text.Json.JsonSerializer.Serialize(keys);
            lock(sync){cancellation.ThrowIfCancellationRequested();if(cached!=null&&ReferenceEquals(guide,guideIdentity)&&signature==input&&now<until)return cached.Select((r,i)=>r with{Key=keys[i]}).ToArray();
                Calculations++;var result=CalculateKeywordCandidates(keys,shows,now,cancellation);cancellation.ThrowIfCancellationRequested();
                guide=guideIdentity;signature=input;until=shows.Where(s=>s.End>now).Select(s=>s.End).DefaultIfEmpty(DateTimeOffset.MaxValue).Min();cached=result;return result;}
        }
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
                var hits=new List<Show>();int count=0;var stationMatches=new Dictionary<string,bool>(StringComparer.Ordinal);var stations=key.Stations.Split(" / ");
                foreach(var show in upcoming){cancellation.ThrowIfCancellationRequested();if(!stationMatches.TryGetValue(show.Station,out bool stationMatch))stationMatches[show.Station]=stationMatch=key.Stations.Length==0||stations.Any(station=>Data.SameStation(station,show.Station));if(!stationMatch||!KeywordTextCandidate(key,show,true))continue;count++;if(hits.Count<8)hits.Add(show);}
                string value=count==0?"取得分に候補なし":$"{count}件｜{hits[0].Start:MM/dd HH:mm} {hits[0].Title}";
                string tooltip="保存済み番組表のタイトル・詳細・局・曜日による参考候補です。時間・ジャンル・放送種別やTVRock固有の検索方式は未反映です。\n"+string.Join("\n",hits.Select(s=>$"{s.Start:MM/dd HH:mm} {s.Station} {s.Title}"));
                results.Add(new(key,value,tooltip));
            } catch(Exception e) when(e is ArgumentException or InvalidDataException or System.Text.RegularExpressions.RegexMatchTimeoutException) {results.Add(new(key,"検索条件を確認","この検索条件の参考候補を計算できません。条件を編集をご利用ください。"));}
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
        var keys=rows.Select(r=>(KeywordEntry)r.Tag!).ToArray();var guideIdentity=_nativeShows;var shows=_nativeShows.ToArray();var now=DateTimeOffset.Now;
        // Keep the previous result visible until a changed result is ready.
        try
        {
            var results=await Task.Run(()=>keywordCandidateCache.Get(keys,shows,guideIdentity,now,cancellation.Token),cancellation.Token);
            if(IsDisposed||version!=keywordCandidateVersion||cancellation.IsCancellationRequested)return;
            for(int i=0;i<results.Length;i++)
            {
                if(rows[i].DataGridView!=_nativeKeywords||!ReferenceEquals(rows[i].Tag,results[i].Key))continue;
                var cell=rows[i].Cells["candidates"];if(!Equals(cell.Value,results[i].Value))cell.Value=results[i].Value;if(cell.ToolTipText!=results[i].Tooltip)cell.ToolTipText=results[i].Tooltip;
            }
        }
        catch(OperationCanceledException){}
        catch(Exception error){if(!IsDisposed&&version==keywordCandidateVersion)_nativeStatus.Text="候補を更新できません："+error.Message;}
        finally{if(ReferenceEquals(keywordCandidateCancellation,cancellation))keywordCandidateCancellation=null;}
    }
}
