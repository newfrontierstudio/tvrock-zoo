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
                var reservations=(DataGridView)Field("_nativeReservations");
                int rowIndex=reservations.Rows.Add("○",running.Start.LocalDateTime,3,"NHK","manual","test");var original=reservations.Rows[rowIndex];original.Tag=running;
                form.RefreshReservationClock(now);Check(form.RefreshReservationClock(now.AddSeconds(1))==0,"Unchanged clock tick must not repaint rows");
                Check(ReferenceEquals(original,reservations.Rows[0]),"Clock update preserves the row instance");
                Check(form.RefreshReservationClock(running.End)>0&&reservations.Rows.Count==0,"Recording expiration updates without rebuilding live rows");
                var touched=running with{Fields=new(){{"IGNORED_WRITE_METADATA","changed"}}};
                Check(MainForm.ReservationDisplayFingerprint(new[]{running},Array.Empty<MainForm.AiEntry>())==MainForm.ReservationDisplayFingerprint(new[]{touched},Array.Empty<MainForm.AiEntry>()),"Metadata-only file writes do not rebuild the view");
                Check(MainForm.ReservationDisplayFingerprint(new[]{running},Array.Empty<MainForm.AiEntry>())!=MainForm.ReservationDisplayFingerprint(new[]{running with{Enabled=false}},Array.Empty<MainForm.AiEntry>()),"Real reservation state change still refreshes view");
            }
            var tastes=Enumerable.Range(0,127).Select(i=>new Taste("工場見学 金属部品 職人の技術 "+i,1)).ToList();
            string[] stationNames={"NHK","日テレ","テレ朝","TBS","テレ東","フジ","Eテレ","MX"};
            using(var badgeImage=new Bitmap(760,294))using(var graphics=Graphics.FromImage(badgeImage))using(var rowFont=new Font("Meiryo UI",10)){
                graphics.Clear(Color.White);
                for(int i=0;i<stationNames.Length;i++){
                    int y=38+i*29;var bounds=new Rectangle(210,y,78,29);var fill=StationBadgeRenderer.Background(stationNames[i]);Check(fill.R>210&&fill.G>210&&fill.B>210,"Station background should remain subtle");
                    using(var row=new SolidBrush(i%2==0?Color.White:Color.FromArgb(245,248,252)))graphics.FillRectangle(row,0,y,760,29);
                    TextRenderer.DrawText(graphics,"10/17(土) 18:00   30分",rowFont,new Rectangle(8,y,196,29),Color.FromArgb(23,42,62),TextFormatFlags.VerticalCenter);
                    int width=StationBadgeRenderer.Draw(graphics,stationNames[i],rowFont,bounds);Check(width>0&&width<=bounds.Width,"Station badge fits within unchanged row and column");
                    TextRenderer.DrawText(graphics,new[]{"経済バックヤード","バラエティ特番","ドラえもん","報道特集","街歩きの旅","MUSIC FAIR","デザインあneo","探偵！ナイトスクープ"}[i],rowFont,new Rectangle(300,y,452,29),Color.FromArgb(23,42,62),TextFormatFlags.VerticalCenter);
                }
                using(var heading=new SolidBrush(Color.FromArgb(230,237,246)))graphics.FillRectangle(heading,0,0,760,34);
                TextRenderer.DrawText(graphics,"開始日時                 長さ       局          番組名",rowFont,new Rectangle(8,0,744,34),Color.FromArgb(23,42,62),TextFormatFlags.VerticalCenter);
                if(args.Length>1)badgeImage.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
            }
            using(var button=new Button{Size=new Size(120,36)}){
                RoundedControls.Apply(button);Check(button.Region!=null&&!button.Region.IsVisible(0,0)&&button.Region.IsVisible(60,18),"Rounded button keeps its center and clips corners");
                button.Size=new Size(180,42);Check(button.Region!.IsVisible(175,20)&&!button.Region.IsVisible(179,0),"Rounded button region follows resized bounds");
                int clicked=0;button.Click+=(_,_)=>clicked++;button.PerformClick();Check(clicked==1,"Rounded button retains normal click behavior");
            }
            using(var tabs=new FlatTabControl{Size=new Size(920,140),DrawMode=TabDrawMode.OwnerDrawFixed,SizeMode=TabSizeMode.Fixed,ItemSize=new Size(170,35)})using(var image=new Bitmap(920,140)){
                foreach(string name in new[]{"予約一覧  ▼","番組表・検索","自動検索予約","詳細設定","HELP"})tabs.TabPages.Add(new TabPage(name){BackColor=Color.White});
                tabs.DrawItem+=(_,e)=>{var rect=e.Bounds;rect.Inflate(-3,-3);using var path=RoundedControls.Shape(rect,4);using var fill=new SolidBrush(e.Index==tabs.SelectedIndex?Color.FromArgb(29,91,160):Color.FromArgb(232,238,245));e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.FillPath(fill,path);TextRenderer.DrawText(e.Graphics,tabs.TabPages[e.Index].Text,tabs.Font,rect,e.Index==tabs.SelectedIndex?Color.White:Color.FromArgb(23,42,62),TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);};
                tabs.CreateControl();tabs.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
                var inactive=tabs.GetTabRect(1);Check(image.GetPixel(inactive.Right-2,inactive.Top+4).ToArgb()==Color.White.ToArgb(),"Inactive tab has no native vertical edge");
                tabs.SelectedIndex=1;Check(tabs.SelectedIndex==1,"Custom-painted tabs remain selectable");
                if(args.Length>2)image.Save(args[2],System.Drawing.Imaging.ImageFormat.Png);
            }
            Check(MainForm.TimingSeries("情報7daysニュースキャスター 異例の速報")=="情報7daysニュースキャスター","Series default drops episode-specific subtitle");
            Check(MainForm.TimingSeries("MUSIC FAIR")=="MUSIC FAIR","Series default preserves multiword Latin title");
            var timingRule=new TimingRule("TBS","情報7days",-10,20);
            var timingBooking=running with{Id="timing",Station="TBS",Title="情報7daysニュースキャスター 特集",Fields=new(){{"READY","0"},{"TALE","0"}}};
            Check(TimingSettings.Matches(timingRule,timingBooking),"Series matches an existing reservation");
            Check(!TimingSettings.Matches(timingRule,timingBooking with{Station="テレビ朝日"}),"Timing policy remains scoped to station");
            var settings=new TimingSettings{Rules=[timingRule]};Check(settings.Desired(timingBooking)==(-10,20),"Existing reservation receives always policy");
            Check(settings.Desired(timingBooking with{Id="future",Start=now.AddDays(7)})==(-10,20),"New future reservation receives same absolute policy");
            settings.Once.Add(new("timing",5,-5));Check(settings.Desired(timingBooking)==(5,-5),"One-off override takes precedence for its reservation");
            var baseFields=new Dictionary<string,string>{{"i","timing"},{"title","fixture"},{"ready","0"},{"tale","0"},{"year","2026"},{"devno","5"},{"valid","true"}};
            var changed=RecordingTiming.BuildFields(baseFields,-10,20);Check(changed["ready"]=="10"&&changed["tale"]=="20"&&changed["year"]=="2026"&&changed["devno"]=="5"&&changed["valid"]=="true"&&baseFields["ready"]=="0","Offsets preserve time, tuner and validity fields");
            var fake=timingBooking with{Title="fixture"};int sends=0;
            Task<string> FakeGet(string relative){if(relative.StartsWith("reg?"))return Task.FromResult("<form name=\u0027regsch\u0027><input name='i' value='timing'><input name='title' value='fixture'><input name='ready' value='0'><input name='tale' value='0'><input name='valid' type='checkbox' value='true' checked><input type='submit' name='submit' value='予約を変更'></form>");sends++;var q=Data.Query(relative);fake=fake with{Fields=new(){{"READY",q["ready"]},{"TALE",q["tale"]}}};throw new HttpRequestException("synthetic response reset after successful send");}
            RecordingTiming.Apply(fake,-10,20,()=>[fake],FakeGet,()=>{}).GetAwaiter().GetResult();
            RecordingTiming.Apply(fake,-10,20,()=>[fake],FakeGet,()=>{}).GetAwaiter().GetResult();
            Check(sends==1&&RecordingTiming.Current(fake)==(-10,20),"Response reset is verified without resend; repeated enforcement never adds offsets");
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
