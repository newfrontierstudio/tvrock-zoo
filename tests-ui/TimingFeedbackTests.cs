using TsScheduler;using TsRecommend;
internal static class TimingFeedbackTests{
 internal static void Run(){
 void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
 Check(KeywordTiming.Read(new Dictionary<string,string>{{"REDAY","-27"},{"READY","0"}})==(27,0),"Wednesday +27 seconds uses TVRock keyword REDAY");
 Check(KeywordTiming.Read(new Dictionary<string,string>{{"REDAY","-35"},{"TALE","0"}})==(35,0),"Appraisal programme +35 seconds preserved");
 Check(KeywordTiming.Read(new Dictionary<string,string>{{"READY","30"},{"TALE","-10"}})==(-30,-10),"READY compatibility and end offset retained");
 var start=new DateTimeOffset(2026,10,28,17,0,0,TimeSpan.FromHours(9));var b=new Booking("wed","[生]５時に夢中！ 特集[字]","MX",1,1,start,start.AddHours(1),0,true,true,false,new(){["READY"]="-27",["TALE"]="0"});
 var settings=new TimingSettings{Rules=[new("MX","５時に夢中！",0,-10),new("MX","５時に夢中！",27,0,8)]};
 Check(settings.Desired(b)==(27,0),"Wednesday-specific timing overrides general rule despite broadcast marks");
 Check(settings.Desired(b with{Start=start.AddDays(1),End=start.AddDays(1).AddHours(1)})==(0,-10),"Other weekday retains its own timing");
 settings.Once.Add(new(b.Id,35,-5));Check(settings.Desired(b)==(35,-5),"Individual user timing wins over recurring rules");
 Check(RecordingTiming.Current(b)==(27,0),"Individual reservation uses READY without zeroing it");
 var fields=RecordingTiming.BuildFields(new(){["i"]="one",["READY"]="-27",["unknown"]="keep"},27,0);Check(fields["ready"]=="-27"&&fields["unknown"]=="keep","Timing form preserves unrelated fields");
 }
}
