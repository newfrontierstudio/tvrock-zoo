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
    }
}
