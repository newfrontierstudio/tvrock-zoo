using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TsRecommend;
using TsScheduler;
using Zoo;

static class Bench
{
    static void Check(bool ok,string reason) {if(!ok)throw new Exception(reason);}
    static object Time(string name,int runs,Action work) {
        work();var samples=new List<double>();for(int i=0;i<runs;i++){var watch=Stopwatch.StartNew();work();watch.Stop();samples.Add(watch.Elapsed.TotalMilliseconds);}samples.Sort();return new{name,runs,medianMs=samples[samples.Count/2],maxMs=samples.Max()};
    }
    [STAThread] static int Main(string[] args) {
        try {
            ApplicationConfiguration.Initialize();Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Check(RuntimeConfiguration.NormalizeUrl("http://localhost:8969/test/list?x=1")=="http://localhost:8969/test/","URL normalization");
            Check(RuntimeConfiguration.NormalizeUrl($"http://{Environment.MachineName}:8969/test/list")=="http://127.0.0.1:8969/test/","Local computer URL normalization");
            Check(RuntimeConfiguration.NormalizeUrl("http://remote.invalid:8969/test/")=="http://remote.invalid:8969/test/","Remote host must not become localhost");
            Check(typeof(RuntimeConfiguration).GetProperty("ExcludeNhkPromos")==null,"Personal PR setting remains");
            using(var setup=new SetupDialog()){Check(!Descendants(setup).Any(c=>c.Text.Contains("PR予約")),"PR checkbox remains");}
            var day=new DateTimeOffset(DateTime.Today,TimeSpan.FromHours(9));
            var shows=Enumerable.Range(0,3000).Select(i=>new Show("show"+i,1+i%8,i,"局"+(i%8),"工場見学 金属部品の秘密 "+i,"職人の技術と工場の新しい挑戦を紹介します。",day.AddMinutes((i/8)*30),day.AddMinutes((i/8)*30+30),new(){{5,"day?r=1&c=1&e="+i+"&d=5"}}){GenreColor="#ffdddd"}).ToList();
            var candidateKey=new MainForm.KeywordEntry(1,"test",true,"工場 not PR","職人","局0","日月火水木金土","");
            Check(MainForm.KeywordTextCandidate(candidateKey,shows[0]),"Keyword candidate matches title/detail/station/day");
            Check(!MainForm.KeywordTextCandidate(candidateKey,shows[0] with{Title="工場PR"}),"Keyword candidate excludes NOT term");
            Check(!MainForm.KeywordTextCandidate(candidateKey,shows[0] with{Station="別局"}),"Keyword candidate excludes different station");
            Check(MainForm.KeywordTextCandidate(candidateKey with{Stations="テレビ朝日"},shows[0] with{Station="テレ朝"}),"TV Asahi aliases match");
            Check(!Data.SameStation("BS朝日","テレビ朝日"),"BS Asahi remains separate");
            string stationFixture=Path.GetTempFileName();
            try{File.WriteAllText(stationFixture,"[TUNER1]\nAPPNAME1=TVTest.exe\n[TUNER1 SERVICE]\nNHK=1024\n[TUNER2]\nAPPNAME1=\n[TUNER2 SERVICE]\nテレビ朝日=1064\n",Data.Cp932);var parsed=Data.Tuners(stationFixture);Check(parsed.Stations.ContainsKey(1064),"Guide stations include configured services on inactive tuners");Check(parsed.Tuners.Count==1,"Inactive tuner is not usable for recording");}finally{File.Delete(stationFixture);}
            string switchHome=Path.Combine(Path.GetTempPath(),"zoo-switch-"+Guid.NewGuid());
            try{RecommendationSwitch.Save(switchHome,false);Check(File.Exists(Path.Combine(switchHome,"recommend-disabled.txt")),"Recommendation OFF persisted");RecommendationSwitch.Save(switchHome,true);Check(!File.Exists(Path.Combine(switchHome,"recommend-disabled.txt")),"Recommendation ON persisted");}finally{if(Directory.Exists(switchHome))Directory.Delete(switchHome);}
            var now=day.AddHours(12);
            var running=new Booking("running","test","NHK",1,1,now.AddMinutes(-1),now.AddSeconds(2),5,true,true,false,new());
            var upcoming=running with{Id="next",Start=now.AddSeconds(5),End=now.AddMinutes(30)};
            Check(MainForm.NextReservationBoundary(new[]{running,upcoming},now)==running.End,"Recording end triggers refresh without a file change");
            Check(MainForm.NextReservationBoundary(new[]{running,upcoming},running.End)==upcoming.Start,"Next start remains scheduled after recording ends");
            Check(!MainForm.ReservationRows(new[]{running},false,false,running.End).Any(),"Ended recording leaves active list at end boundary");
            Check(MainForm.NextReservationBoundary(new[]{running},running.End)==DateTimeOffset.MaxValue,"Expired bookings do not cause continuous reloads");
            using(var form=new MainForm(false)){
                object Field(string name)=>typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
                var caption=(Button)Field("_foldDisabled");var panel=(Panel)Field("_disabledBox");var split=(SplitContainer)Field("_reservationSplit");
                foreach(float size in new[]{10f,18f,24f}){using var font=new Font("Meiryo UI",size);caption.Font=font;panel.PerformLayout();Check(caption.Height>=caption.Font.Height+8,"Disabled caption must fit enlarged font");}
                Check(((System.Windows.Forms.Timer)Field("_nativeTimer")).Interval==1000,"Reservation file changes checked every second");
            }
            var tastes=Enumerable.Range(0,127).Select(i=>new Taste("工場見学 金属部品 職人の技術 "+i,1)).ToList();
            var measurements=new List<object>();
            measurements.Add(Time("rank-3000-programs-127-preferences",5,()=>{var ranker=new Ranker(tastes);foreach(var s in shows)ranker.Score(s);}));
            string fixture=Path.GetTempFileName();
            try {var text=new StringBuilder();for(int i=0;i<500;i++)text.Append($"{i} START {day.AddMinutes(i).ToUnixTimeSeconds()}\n{i} END {day.AddMinutes(i+30).ToUnixTimeSeconds()}\n{i} UNIQID {i+1}\n{i} VALIDATE 1\n{i} TITLE 合成番組{i}\n");File.WriteAllText(fixture,text.ToString(),Data.Cp932);Check(Data.Bookings(fixture).Count==500,"Reservation parser count");measurements.Add(Time("read-500-reservations",20,()=>Data.Bookings(fixture)));}finally{File.Delete(fixture);}
            using(var grid=new ScheduleGrid{Size=new Size(1200,800),Shows=shows,Day=day.Date})using(var bitmap=new Bitmap(1200,800)){
                grid.CreateControl();grid.RefreshSchedule();Check(grid.VisibleDays==1&&grid.DurationMinutes==1440,"24h bounds");Check(ScheduleGrid.TimelineY(480)-ScheduleGrid.TimelineY(479)==33,"8h header position");
                var fill=ScheduleGrid.CategoryFill(shows[0]);Check(fill.R>=240&&fill.G>=240&&fill.B>=240,"Background too dark");
                measurements.Add(Time("paint-and-scroll-3000-programs",30,()=>{grid.AutoScrollPosition=new Point(0,(Environment.TickCount&1023)*3);grid.DrawToBitmap(bitmap,new Rectangle(0,0,1200,800));}));
            }
            var process=Process.GetCurrentProcess();var result=new{passed=true,measurements,workingSetMiB=process.WorkingSet64/1048576.0,note="Synthetic local benchmark; excludes TVRock HTTP, cold start and WebView2. No live reservations changed."};
            File.WriteAllText(args.Length > 0 ? args[0] : "benchmark-results.json",JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(result));return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static IEnumerable<Control> Descendants(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Descendants(x)));
}
