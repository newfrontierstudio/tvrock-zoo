using TsScheduler;
using TsRecommend;
static class CommonBehaviorTests
{
    internal static void Run()
    {
        void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
        string home=Path.Combine(Path.GetTempPath(),"zoo-common-"+Guid.NewGuid().ToString("N"));
        Check(!GestureOptions.Load(home).Enabled,"Shared gestures default OFF");
        var options=new GestureOptions{Enabled=true};options.Save(home);
        Check(GestureOptions.Load(home).ActionFor("Vertical")=="Refresh","Shared gesture settings round trip");
        Check(MainForm.BackGestureFilter.GestureDirection(new(100,100),new(100,100),20,100,96)=="Vertical","Up/down return stroke refreshes");
        Check(RerunPreference.IsRerun("[再] 番組")&&!RerunPreference.IsRerun("再会の番組"),"Rerun marker excludes ordinary words");
        var now=DateTimeOffset.Now;
        var rule=new MainForm.KeywordEntry(1,"synthetic",true,"合成旅","","","","");
        var show=new Show("synthetic",1,1,"局","合成旅の番組","詳細",now.AddHours(1),now.AddHours(2),[]);
        var candidates=MainForm.CalculateKeywordCandidates([rule],[show],now,CancellationToken.None);
        Check(candidates.Length==1&&candidates[0].Value.StartsWith("1件")&&candidates[0].Tooltip.Contains(show.Title),"Background candidate calculation preserves count and detail");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;
        try{MainForm.CalculateKeywordCandidates([rule],[show],now,cancelled.Token);}catch(OperationCanceledException){stopped=true;}
        Check(stopped,"Superseded candidate refresh can be cancelled");
        var cache=new MainForm.KeywordCandidateCache();var guide=new[]{show};var first=cache.Get([rule],guide,guide,now,CancellationToken.None);
        var cloned=rule with{};var again=cache.Get([cloned],guide,guide,now.AddSeconds(1),CancellationToken.None);
        Check(cache.Calculations==1&&again[0].Value==first[0].Value&&ReferenceEquals(again[0].Key,cloned),"Unchanged guide/rules reuse results and rebind current UI rows");
        Check(cache.Get([rule with{Title="一致しない"}],guide,guide,now,CancellationToken.None)[0].Value=="取得分に候補なし"&&cache.Calculations==2,"Edited rule invalidates candidate cache");
        cache.Get([rule],guide,guide,now,CancellationToken.None);var expired=cache.Get([rule],guide,guide,now.AddHours(3),CancellationToken.None);
        Check(expired[0].Value=="取得分に候補なし"&&cache.Calculations==4,"Programme end expires cached candidates");
        var fresh=new[]{show with{Title="別の番組"}};Check(cache.Get([rule],fresh,fresh,now,CancellationToken.None)[0].Value=="取得分に候補なし"&&cache.Calculations==5,"New guide invalidates cached matches");

    }
}
