using TsRecommend;
namespace TsScheduler;
internal static class GuideReservationTransaction
{
    internal static Show Verify(Show requested,IEnumerable<Show> live,int device)
    {
        // Titles/details may be refreshed independently of the event identity.
        var matches=live.Where(s=>s.Key==requested.Key&&s.ServiceId==requested.ServiceId&&s.EventId==requested.EventId&&s.Start==requested.Start&&s.End==requested.End&&Data.SameStation(s.Station,requested.Station)).ToArray();
        if(matches.Length!=1||!matches[0].Actions.TryGetValue(device,out var action))throw new InvalidOperationException("放送内容が変わりました。番組表を更新してください。");
        var q=Data.Query(action);
        if(q.GetValueOrDefault("c")!=requested.ServiceId.ToString()||q.GetValueOrDefault("e")!=requested.EventId.ToString()||q.GetValueOrDefault("d")!=device.ToString()||q.GetValueOrDefault("r")!="1")throw new InvalidDataException("予約リンクの照合に失敗しました。");
        return matches[0];
    }
    internal static async Task Send(Show show,int device,Func<string,Task<string>> get,Func<List<Booking>> read,Func<Task>? delay=null)
    {
        var current=Verify(show,[show],device);
        Exception? transportError=null;
        // TVRock may have saved the booking even if the HTTP response was lost.
        try{await get(current.Actions[device]);}catch(Exception error)when(error is HttpRequestException or TaskCanceledException){transportError=error;}
        for(int attempt=0;attempt<20;attempt++){
            if(read().Any(b=>b.Enabled&&b.Device==device&&Planner.SameBroadcast(show,b)))return;
            await (delay?.Invoke()??Task.Delay(250));
        }
        throw new IOException("予約の反映を確認できません。自動再送はしません。再試行する前に予約一覧をご確認ください。",transportError);
    }
}
