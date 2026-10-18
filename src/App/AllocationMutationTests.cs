using TsRecommend;
namespace TsScheduler;
internal static class AllocationMutationTests
{
    internal static async Task Run(string root)
    {
        var now=new DateTimeOffset(2030,1,1,8,0,0,TimeSpan.FromHours(9));var start=now.AddHours(2);
        var first=new Booking("one","番組A","局A",1,1,start,start.AddMinutes(30),5,true,true,false,new(){["READY"]="120",["TALE"]="30",["DEVNO"]="5"});
        var rows=new List<Booking>{first};int writes=0,backups=0;
        string Form(Booking b){var local=b.Start.ToLocalTime();var end=b.End.ToLocalTime();var fields=new Dictionary<string,string>{{"i",b.Id},{"title",b.Title},{"station",b.Station},{"devno",b.Device.ToString()},{"year",local.Year.ToString()},{"mon",local.Month.ToString()},{"day",local.Day.ToString()},{"shour",local.Hour.ToString()},{"smin",local.Minute.ToString()},{"ehour",end.Hour.ToString()},{"emin",end.Minute.ToString()},{"ready","120"},{"tale","30"}};return "<form name='regsch'>"+string.Concat(fields.Select(p=>$"<input name='{p.Key}' value='{p.Value}'>"))+"<input type='checkbox' name='valid' value='true' checked><input type='submit' name='submit' value='予約を変更'></form>";}
        Task<string> Get(string action){if(action.StartsWith("reg?"))return Task.FromResult(Form(rows.Single(b=>b.Id==Data.Query(action)["i"])));writes++;var query=Data.Query(action);int index=rows.FindIndex(b=>b.Id==query["i"]);var b=rows[index];var fields=new Dictionary<string,string>(b.Fields){["DEVNO"]=query["devno"]};rows[index]=b with{Device=int.Parse(query["devno"]),Fields=fields};return Task.FromResult("");}
        var plan=new AllocationChoice(5,new(){{"one",6},{"$new",5}},["one"]);
        await MainForm.ApplyNativeAllocationCore(plan,rows.ToList(),()=>rows.ToList(),Get,_=>backups++,()=>now,root);
        if(writes!=1||backups!=1||rows[0].Device!=6||rows[0].Start!=first.Start||rows[0].End!=first.End||rows[0].Title!=first.Title)throw new Exception("Native relocation must only change the tuner");
        rows=[first with{Start=now.AddSeconds(30)}];writes=0;try{await MainForm.ApplyNativeAllocationCore(plan,rows.ToList(),()=>rows.ToList(),Get,_=>backups++,()=>now,root);throw new Exception("Near-start mutation must fail");}catch(InvalidOperationException){}if(writes!=0)throw new Exception("Near-start reservation was written");
        rows=[first];var expected=rows.ToList();rows=[first with{Title="changed"}];try{await MainForm.ApplyNativeAllocationCore(plan,expected,()=>rows.ToList(),Get,_=>backups++,()=>now,root);throw new Exception("Stale plan must fail");}catch(InvalidOperationException){}if(writes!=0)throw new Exception("Stale plan was written");
        rows=[first];Task<string> WrongForm(string action)=>Task.FromResult(Form(first).Replace("value='番組A'","value='different'"));try{await MainForm.ApplyNativeAllocationCore(plan,rows.ToList(),()=>rows.ToList(),WrongForm,_=>backups++,()=>now,root);throw new Exception("Mismatched form must fail");}catch(InvalidDataException){}
        var second=first with{Id="two",Title="番組B",Device=6,Fields=new(first.Fields){["DEVNO"]="6"}};rows=[first,second];writes=0;
        Task<string> Interrupted(string action){if(action.StartsWith("reg?"))return Get(action);if(++writes==2)throw new HttpRequestException("synthetic connection failure");var q=Data.Query(action);int index=rows.FindIndex(b=>b.Id==q["i"]);var b=rows[index];rows[index]=b with{Device=int.Parse(q["devno"]),Fields=new(b.Fields){["DEVNO"]=q["devno"]}};return Task.FromResult("");}
        try{await MainForm.ApplyNativeAllocationCore(new(5,new(){{"one",7},{"two",8}},["one","two"]),rows.ToList(),()=>rows.ToList(),Interrupted,_=>backups++,()=>now,root);throw new Exception("Interrupted multi-change must report partial progress");}catch(IOException e){if(!e.Message.Contains("1/2"))throw;}
        if(writes!=2||rows[0].Device!=7||rows[1].Device!=6)throw new Exception("Interrupted relocation must stop without retrying writes");
    }
}
