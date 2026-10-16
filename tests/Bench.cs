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
            Check(typeof(RuntimeConfiguration).GetProperty("ExcludeNhkPromos")==null,"Personal PR setting remains");
            using(var setup=new SetupDialog()){Check(!Descendants(setup).Any(c=>c.Text.Contains("PR予約")),"PR checkbox remains");}
            var day=new DateTimeOffset(DateTime.Today,TimeSpan.FromHours(9));
            var shows=Enumerable.Range(0,3000).Select(i=>new Show("show"+i,1+i%8,i,"局"+(i%8),"工場見学 金属部品の秘密 "+i,"職人の技術と工場の新しい挑戦を紹介します。",day.AddMinutes((i/8)*30),day.AddMinutes((i/8)*30+30),new(){{5,"day?r=1&c=1&e="+i+"&d=5"}}){GenreColor="#ffdddd"}).ToList();
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
