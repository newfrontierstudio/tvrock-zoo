using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    internal static bool ReservationDeviceAvailable(Show show,List<Tuner> tuners,List<Booking> bookings,int device)=>Planner.FreeDevice(show,tuners.OrderBy(t=>t.Device==device?0:1).ToList(),bookings.Where(b=>b.Enabled).ToList(),[])==device;
    internal static string AllocationStamp(IEnumerable<Booking> bookings)=>System.Text.Json.JsonSerializer.Serialize(bookings.OrderBy(b=>b.Id,StringComparer.Ordinal).Select(b=>new{b.Id,b.Title,b.Station,b.ServiceId,b.EventId,b.Start,b.End,b.Device,b.Enabled,b.Repeating,b.Fields}));
    internal static AllocationTask AllocationBooking(Booking b,List<Tuner> tuners,DateTimeOffset now)
    {
        int.TryParse(b.Fields.GetValueOrDefault("READY"),out int ready);int.TryParse(b.Fields.GetValueOrDefault("TALE"),out int tail);
        bool movable=b.Start.AddSeconds(-ready)>now.AddMinutes(2)&&!b.Repeating&&b.Start.Second==0&&b.End.Second==0&&new[]{"WATCH","ONESEG","EPGMODE","REMOTE"}.All(key=>!b.Fields.TryGetValue(key,out string? flag)||flag=="0")&&(!b.Fields.TryGetValue("COMPNAME",out string? computer)||computer.Length==0||computer.Equals(Environment.MachineName,StringComparison.OrdinalIgnoreCase));
        int[] candidates=movable?tuners.Where(t=>t.Stations.Any(s=>Data.SameStation(s,b.Station))).Select(t=>t.Device).ToArray():[b.Device];
        return new(b.Id,b.Title,b.Start.AddSeconds(-ready),b.End.AddSeconds(tail),b.Device,candidates,movable);
    }
    AllocationChoice? ChooseNativeAllocation(Show show,List<Tuner> tuners,List<Booking> bookings,Dictionary<int,int> deviceIds,int ready,int tail)
    {
        var added=new AllocationTask("$new",show.Title,show.Start.AddSeconds(-ready),show.End.AddSeconds(tail),-1,tuners.Where(t=>show.Actions.ContainsKey(t.Device)).Select(t=>t.Device).ToArray(),true);
        var active=bookings.Where(b=>b.Enabled).Select(b=>AllocationBooking(b,tuners,DateTimeOffset.Now)).ToList();
        if(TunerAllocation.Connected(added,active).Any(b=>b.Id!="$new"&&!tuners.Any(t=>t.Device==b.Current)))throw new InvalidOperationException("同じ時間帯に割り当てを確認できない予約があります。接続・チューナー設定を確認してください。");
        string Label(int d){int number=deviceIds.FirstOrDefault(p=>p.Value==d).Key;return number>0?$"チューナー {number}":"不明";}
        return TunerAllocation.Choose(this,added,active,Label);
    }
        Task ApplyNativeAllocation(AllocationChoice plan,List<Booking> expected)=>ApplyNativeAllocationCore(plan,expected,()=>Data.Bookings(Path.Combine(RockFolder,"tvrock.sch")),NativeGet,folder=>File.Copy(Path.Combine(RockFolder,"tvrock.sch"),Path.Combine(folder,"before.sch")),()=>DateTimeOffset.Now,Path.Combine(RecommendationHome,"allocation-history"));
    internal static async Task ApplyNativeAllocationCore(AllocationChoice plan,List<Booking> expected,Func<List<Booking>> read,Func<string,Task<string>> get,Action<string> backupSource,Func<DateTimeOffset> clock,string history)
    {
        if(AllocationStamp(read())!=AllocationStamp(expected))throw new InvalidOperationException("予約状況が変わりました。もう一度選んでください。");
        var changes=expected.Where(b=>plan.Assignments.TryGetValue(b.Id,out int d)&&d!=b.Device).ToList();if(changes.Count==0)return;
        var forms=new Dictionary<string,Dictionary<string,string>>();
        foreach(var b in changes)
        {
            if(b.Repeating||b.Start.AddSeconds(RecordingTiming.Current(b).Head)<=clock().AddMinutes(2))throw new InvalidOperationException("開始直前の予約は再配置できません。");
            var fields=Data.Form(await get("reg?i="+Uri.EscapeDataString(b.Id)),"予約を変更");
            if(fields.GetValueOrDefault("i")!=b.Id||Data.Norm(fields.GetValueOrDefault("title",""))!=Data.Norm(b.Title)||fields.GetValueOrDefault("devno")!=b.Device.ToString())throw new InvalidDataException("再配置する予約フォームを照合できませんでした。");
                        if(b.Start.Second!=0||b.End.Second!=0)throw new InvalidOperationException("秒単位の予約はTVRock側で再配置してください。");
            int Number(string key)=>int.TryParse(fields.GetValueOrDefault(key),out int value)?value:-1;
            var start=b.Start.ToLocalTime();var end=b.End.ToLocalTime();
            if(Number("year")!=start.Year||Number("mon")!=start.Month||Number("day")!=start.Day||Number("shour")!=start.Hour||Number("smin")!=start.Minute||Number("ehour")!=end.Hour||Number("emin")!=end.Minute||Number("ready")!=-RecordingTiming.Current(b).Head||Number("tale")!=RecordingTiming.Current(b).Tail||!Data.SameStation(fields.GetValueOrDefault("station",""),b.Station)||fields.ContainsKey("valid")!=b.Enabled)throw new InvalidDataException("予約フォームの日時・局・録画設定が一致しないため停止しました。");
            fields["devno"]=plan.Assignments[b.Id].ToString();forms[b.Id]=fields;
        }
        if(AllocationStamp(read())!=AllocationStamp(expected))throw new InvalidOperationException("予約状況が変わりました。再配置は実行していません。");
        string backup=Path.Combine(history,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);backupSource(backup);
        var applied=new List<string>();var snapshot=expected;
        try
        {
            foreach(var b in changes)
            {
                if(AllocationStamp(read())!=AllocationStamp(snapshot))throw new IOException("再配置中に予約状況が変わりました。");
                if(b.Start.AddSeconds(RecordingTiming.Current(b).Head)<=clock().AddMinutes(2))throw new IOException("予約の開始が近づいたため停止しました。");
                var fields=forms[b.Id];Exception? sendError=null;try{await get("regsch?"+string.Join("&",fields.Select(p=>Data.Encode(p.Key)+"="+Data.Encode(p.Value))));}catch(Exception e)when(e is HttpRequestException or TaskCanceledException){sendError=e;}
                bool confirmed=false;for(int n=0;n<16;n++){await Task.Delay(250);var after=read();var row=after.SingleOrDefault(x=>x.Id==b.Id);if(row!=null&&row.Device==plan.Assignments[b.Id]&&row.Start==b.Start&&row.End==b.End&&row.Enabled==b.Enabled&&row.Title==b.Title&&row.Repeating==b.Repeating&&AllocationStamp(after.Where(x=>x.Id!=b.Id))==AllocationStamp(snapshot.Where(x=>x.Id!=b.Id))&&System.Text.Json.JsonSerializer.Serialize(row.Fields.Where(p=>p.Key!="DEVNO"))==System.Text.Json.JsonSerializer.Serialize(b.Fields.Where(p=>p.Key!="DEVNO"))){snapshot=after;confirmed=true;break;}}
                if(!confirmed)throw new IOException("再配置の反映を確認できません。自動再送はしません。",sendError);applied.Add(b.Id);File.WriteAllText(Path.Combine(backup,"applied.json"),System.Text.Json.JsonSerializer.Serialize(applied));
            }
        }
        catch(Exception error){throw new IOException($"{error.Message}\n再配置確認済み {applied.Count}/{changes.Count}件。TVRockの変更は一括保存ではないため、予約一覧を確認してください。新しい予約はまだ登録していません。",error);}
    }
}
