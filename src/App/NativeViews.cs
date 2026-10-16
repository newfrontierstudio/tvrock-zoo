using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TsRecommend;

namespace TsScheduler;

internal sealed partial class MainForm
{
    internal static string RockFolder => Zoo.RuntimeConfiguration.Current.TvRockFolder;
    readonly DataGridView _nativeReservations = NativeGrid();
    readonly DataGridView _nativeGuide = NativeGrid();
    readonly DataGridView _nativeKeywords = NativeGrid();
    readonly TextBox _nativeQuery = new() { Width = 370, PlaceholderText = "例: NHKスペシャル not (PR|再)" };
    readonly ComboBox _nativeStation = new() { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox _includePast = new() { Text = "終了分も表示", AutoSize = true };
    readonly CheckBox _includeDisabled = new() { Text = "無効予約も表示", AutoSize = true };
    readonly CheckBox _includeFinishedBookings = new() { Text = "終了分も表示", AutoSize = true };
    readonly Label _nativeStatus = new() { AutoSize = true, Padding = new Padding(4,8,0,0) };
    readonly System.Windows.Forms.Timer _nativeTimer = new() { Interval = 15000 };
    List<Show> _nativeShows = [];
    List<KeywordEntry> _nativeKeys = [];
    List<Booking> _nativeBookings = [];
    List<AiEntry> _nativeAi = [];
    Font? _nativeItalic;
    string _nativeGuideStamp = "";
    bool _guideRefreshing;
    string _reservationReadStamp = "";
    DateTimeOffset _nextGuideAttempt = DateTimeOffset.Now.AddSeconds(45);
    static readonly JsonSerializerOptions NativeJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };
    internal sealed record KeywordEntry(int Order,string Id,bool Enabled,string Title,string Detail,string Stations,string Days,string Hours);
    internal sealed record AiEntry(string ReservationId,string ProgramTitle,string SearchWord,string Reason,DateTimeOffset Start,string Station);
    sealed record GuideCache(DateTimeOffset Fetched,List<Show> Shows);
    static DataGridView NativeGrid() => new() { Dock=DockStyle.Fill, ReadOnly=true, AllowUserToAddRows=false, AllowUserToDeleteRows=false, RowHeadersVisible=false, AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None, SelectionMode=DataGridViewSelectionMode.FullRowSelect, MultiSelect=false, BackgroundColor=Color.White, BorderStyle=BorderStyle.None, AutoGenerateColumns=false, AllowUserToResizeRows=false, ColumnHeadersHeight=32, ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing, RowTemplate={Height=29}, ShowCellToolTips=true };
    static void Col(DataGridView g,string name,string title,int width,bool fill=false) { var c=new DataGridViewTextBoxColumn { Name=name,HeaderText=title,Width=width,SortMode=DataGridViewColumnSortMode.Programmatic }; if(fill){c.AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;c.MinimumWidth=220;}g.Columns.Add(c); }
    void InitializeNativeViews(FlowLayoutPanel toolbar)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Text="TVRock Zoo — 予約・番組表 Ver.0.1.4";
        _openWeb.Text="詳細設定"; _webAddress.Visible=false; _import.Visible=false;_editReservation.Visible=false;_scheduler.Visible=false;
        _engineStatus.Text="録画: TVRock";
        foreach(TabPage p in _tabs.TabPages) p.Text="詳細: "+p.Text;
        _tabs.TabPages.RemoveAt(2);_tabs.TabPages.RemoveAt(1);_tabs.TabPages[0].Text="詳細設定";
        var listPage=new TabPage("予約一覧");var guidePage=new TabPage("番組表・検索");var keysPage=new TabPage("自動検索予約");
        _tabs.TabPages.Insert(0,listPage);_tabs.TabPages.Insert(1,guidePage);_tabs.TabPages.Insert(2,keysPage);_tabs.SelectedIndex=0;
        var refresh=new Button { Text="一覧を更新",AutoSize=true };refresh.Click+=(_,_)=>LoadNativeData();toolbar.Controls.Add(refresh);
        var listbar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(4)};
        var edit=new Button{Text="選択した予約を編集",AutoSize=true}; edit.Click+=async(_,_)=>await NativeEditBooking();
        listbar.Controls.AddRange([edit,_includeDisabled,_includeFinishedBookings]); _includeDisabled.CheckedChanged+=(_,_)=>LoadNativeReservations();_includeFinishedBookings.CheckedChanged+=(_,_)=>LoadNativeReservations();
        listPage.Controls.Add(_nativeReservations);listPage.Controls.Add(listbar);
        Col(_nativeReservations,"enabled","状態",65);Col(_nativeReservations,"start","開始日時",210);Col(_nativeReservations,"end","長さ",90);Col(_nativeReservations,"station","局",80);Col(_nativeReservations,"word","検索ワード",240);Col(_nativeReservations,"title","番組名",500,true);
        _nativeItalic=new Font("Meiryo UI",12,FontStyle.Bold|FontStyle.Italic);
        _nativeReservations.CellPainting+=PaintAiWord;
        _nativeReservations.CellDoubleClick+=async(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex!=0)await NativeEditBooking();};
        var searchbar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(4)};
        _nativeStation.Items.Add("全局");_nativeStation.SelectedIndex=0;
        var search=new Button{Text="検索",AutoSize=true};search.Click+=(_,_)=>RenderNativeGuide();
        var update=new Button{Text="番組表を取得",AutoSize=true};update.Click+=async(_,_)=>await RefreshNativeGuide();
        var book=new Button{Text="選択番組を予約",AutoSize=true};book.Click+=async(_,_)=>await NativeBookShow();
        searchbar.Controls.AddRange([_nativeQuery,_nativeStation,search,_includePast,update,book]);
        _nativeQuery.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;RenderNativeGuide();}};
        _nativeStation.SelectedIndexChanged+=(_,_)=>RenderNativeGuide();_includePast.CheckedChanged+=(_,_)=>RenderNativeGuide();
        var statusbar=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true};statusbar.Controls.Add(_nativeStatus);
        guidePage.Controls.Add(_nativeGuide);guidePage.Controls.Add(searchbar);guidePage.Controls.Add(statusbar);
        Col(_nativeGuide,"start","開始日時",210);Col(_nativeGuide,"end","終了",80);Col(_nativeGuide,"station","局",80);Col(_nativeGuide,"booked","予約",80);Col(_nativeGuide,"title","番組名（再放送等のマークを含む）",600,true);
        _nativeGuide.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&_nativeGuide.Rows[e.RowIndex].Tag is Show s)MessageBox.Show(this,s.Description,s.Title);};
        var keybar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(4)};
        var keySearch=new Button{Text="検索ワードで探す",AutoSize=true};keySearch.Click+=(_,_)=>SearchNativeKeyword();
        var keyEdit=new Button{Text="条件を編集",AutoSize=true};keyEdit.Click+=async(_,_)=>{if(_nativeKeywords.CurrentRow?.Tag is KeywordEntry k)await OpenNativeLegacy("kwl?i="+Uri.EscapeDataString(k.Id));};
        keybar.Controls.AddRange([keySearch,keyEdit]); keysPage.Controls.Add(_nativeKeywords);keysPage.Controls.Add(keybar);
        Col(_nativeKeywords,"order","順",55);Col(_nativeKeywords,"enabled","状態",65);Col(_nativeKeywords,"title","検索ワード",330);Col(_nativeKeywords,"detail","詳細条件",220);Col(_nativeKeywords,"station","局",180);Col(_nativeKeywords,"days","曜日",130);Col(_nativeKeywords,"hours","時間",100);
        _nativeKeywords.CellDoubleClick+=async(_,e)=>{if(e.RowIndex>=0&&_nativeKeywords.Rows[e.RowIndex].Tag is KeywordEntry k)await OpenNativeLegacy("kwl?i="+Uri.EscapeDataString(k.Id));};
        foreach(var g in new[]{_nativeReservations,_nativeGuide,_nativeKeywords}) { g.DefaultCellStyle.Font=new Font("Meiryo UI",10);g.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(243,247,252);g.ColumnHeaderMouseClick+=(_,e)=>SortNativeGrid(g,e.ColumnIndex); }
        InitializeReadableDesign(toolbar);
        _nativeReservations.Columns[0].Visible=true;_nativeReservations.Columns[0].HeaderText="";_nativeReservations.Columns[0].Width=45;
        _nativeReservations.Columns[4].Visible=false;
        _nativeReservations.Columns[5].HeaderText="番組名";
        _nativeKeywords.Columns["hours"].Visible=false;
        InitializeScheduleViews(guidePage);
        InitializeReservationSwitches(listPage);
        InitializeWorkspacePreferences(toolbar,listPage);
        _nativeKeywords.Columns["enabled"].Visible=false;
        foreach(var grid in new[]{_nativeReservations,_disabledReservations,_nativeGuide,_nativeKeywords})grid.CellFormatting+=(_,e)=>{if(e.ColumnIndex>=0&&grid.Columns[e.ColumnIndex].Name=="station"&&e.Value is string text)e.Value=text.Replace("MX1","MX");};
        _nativeStation.FormattingEnabled=true;
        _nativeStation.Format+=(_,e)=>{if(e.ListItem is string text)e.Value=text.Replace("MX1","MX");};
        _nativeTimer.Tick+=async(_,_)=>{if(ReservationStamp()!=_reservationReadStamp)LoadNativeReservations();if(DateTimeOffset.Now>=_nextGuideAttempt&&!_guideRefreshing){_nextGuideAttempt=DateTimeOffset.Now.AddMinutes(30);await RefreshNativeGuide();}};_nativeTimer.Start();
    }
    static readonly Dictionary<DataGridView,(int Column,bool Desc)> NativeSort=[];
    static void SortNativeGrid(DataGridView grid,int index)
    {
        bool desc=NativeSort.TryGetValue(grid,out var previous)&&previous.Column==index&&!previous.Desc;NativeSort[grid]=(index,desc);
        grid.Sort(new NativeRowComparer(index,desc));ApplyDatePartitions(grid);foreach(DataGridViewColumn c in grid.Columns)c.HeaderCell.SortGlyphDirection=c.Index==index?(desc?SortOrder.Descending:SortOrder.Ascending):SortOrder.None;
    }
    sealed class NativeRowComparer(int column,bool desc):System.Collections.IComparer { public int Compare(object? x,object? y){var a=((DataGridViewRow)x!).Cells[column].Value;var b=((DataGridViewRow)y!).Cells[column].Value;int n=a is IComparable c?c.CompareTo(b):0;return desc?-n:n;} }
    internal static List<KeywordEntry> ReadKeywords(string path)
    {
        var result=new List<KeywordEntry>();var fields=new Dictionary<string,string>();int order=0;
        void Add(){if(!fields.ContainsKey("SEARCHID"))return;var stations=fields.Where(p=>p.Key.StartsWith("STATION")).Select(p=>p.Value);int.TryParse(fields.GetValueOrDefault("WEEK"),out int days);string week=string.Join("",Enumerable.Range(0,7).Where(i=>(days&(1<<i))!=0).Select(i=>"日月火水木金土"[i]));result.Add(new(order,fields["SEARCHID"],fields.GetValueOrDefault("VALIDATE")=="1",fields.GetValueOrDefault("TWORD", ""),fields.GetValueOrDefault("DWORD", ""),string.Join(" / ",stations),week,$"{fields.GetValueOrDefault("STHOUR")}:00 / {fields.GetValueOrDefault("DRHOUR")}時間"));}
        foreach(string raw in File.ReadLines(path,Data.Cp932)){var line=raw.TrimEnd('\r');var m=Regex.Match(line,@"^\[KEYWORD(\d+)\]$");if(m.Success){Add();fields=[];order=int.Parse(m.Groups[1].Value)+1;}else{int eq=line.IndexOf('=');if(eq>0)fields[line[..eq]]=line[(eq+1)..];}}Add();return result;
    }
    void LoadNativeData()
    {
        try {
            _nativeKeys=ReadKeywords(Path.Combine(RockFolder,"tvrock.key"));_nativeKeywords.Rows.Clear();
            foreach(var k in _nativeKeys){int i=_nativeKeywords.Rows.Add(k.Order,k.Enabled?"有効":"無効",k.Title,k.Detail,k.Stations.Split(" / ").Length>=6?"全局":k.Stations,k.Days.Length>=5?"毎日":k.Days,k.Hours);_nativeKeywords.Rows[i].Tag=k;_nativeKeywords.Rows[i].Cells[4].ToolTipText=k.Stations;_nativeKeywords.Rows[i].Cells[5].ToolTipText=k.Days;_nativeKeywords.Rows[i].Cells[4].Style.ForeColor=StationColor(k.Stations);}
            LoadNativeReservations();LoadNativeGuideCache();
        } catch(Exception e){_engineStatus.Text="読み込み失敗: "+e.Message;}
    }
    internal static bool IsAiBooking(Booking booking,AiEntry ai) => booking.Id==ai.ReservationId && booking.Station==ai.Station && Math.Abs((booking.Start-ai.Start).TotalSeconds)<90;
    static string ReservationStamp()=>string.Join("/",new[]{Path.Combine(RockFolder,"tvrock.sch"),Path.Combine(RecommendationHome,"ai-recommendations.json")}.Select(p=>File.Exists(p)?File.GetLastWriteTimeUtc(p).Ticks:0));
    void LoadNativeReservations()
    {
        if(_nativeReservations.Columns.Count==0)return;
        try {
            string? selected=(_nativeReservations.CurrentRow?.Tag as Booking)?.Id;
            int first=Math.Max(0,_nativeReservations.FirstDisplayedScrollingRowIndex);
            _nativeBookings=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch"));
            var file=Path.Combine(RecommendationHome,"ai-recommendations.json");_nativeAi=File.Exists(file)?JsonSerializer.Deserialize<List<AiEntry>>(File.ReadAllText(file),NativeJson)??[]:[];
            _nativeReservations.Rows.Clear();
            foreach(var b in _nativeBookings.Where(b=>b.Enabled&&(_includeFinishedBookings.Checked||b.End>DateTimeOffset.Now)).OrderBy(b=>b.Start)){
                var ai=_nativeAi.FirstOrDefault(a=>IsAiBooking(b,a));string key=b.Fields.GetValueOrDefault("SEARCHID","");var keyword=_nativeKeys.FirstOrDefault(k=>k.Id==key);
                string word=keyword==null?"手動":string.IsNullOrWhiteSpace(keyword.Title)?keyword.Detail:keyword.Title;
                int i=_nativeReservations.Rows.Add(b.Enabled?"○":"×",b.Start.LocalDateTime,(int)Math.Round((b.End-b.Start).TotalMinutes),b.Station,ai?.SearchWord??word,b.Title);
                var row=_nativeReservations.Rows[i];row.Tag=b;row.Cells[1].Style.Format="MM/dd(ddd) HH:mm";row.Cells[2].Style.Format="0分";
                if(ai!=null){row.Cells[4].Tag=ai;row.Cells[4].Style.Font=_nativeItalic;row.Cells[5].Style.Font=_nativeItalic;row.Cells[5].Style.ForeColor=Ink;row.Cells[4].ToolTipText="AIおすすめ（PC内で選定）\n"+ai.Reason;}
                row.Cells[3].Style.ForeColor=StationColor(b.Station);
                if(b.Id==selected){row.Selected=true;_nativeReservations.CurrentCell=row.Cells[1];}
            }
            if(NativeSort.TryGetValue(_nativeReservations,out var sort))_nativeReservations.Sort(new NativeRowComparer(sort.Column,sort.Desc));
            if(_nativeReservations.IsHandleCreated&&_nativeReservations.Visible&&_nativeReservations.DisplayedRowCount(false)>0&&_nativeReservations.Rows.Count>first)_nativeReservations.FirstDisplayedScrollingRowIndex=first;
            ApplyDatePartitions(_nativeReservations);
            LoadDisabledReservations();
            _reservationReadStamp=ReservationStamp();
            _engineStatus.Text=$"予約 {_nativeReservations.Rows.Count}件 / 録画: TVRock";
        }catch(IOException e){_engineStatus.Text="予約の再読込待ち: "+e.Message;}catch(JsonException e){_engineStatus.Text="おすすめ記録の読込失敗: "+e.Message;}
    }
    void PaintAiWord(object? sender,DataGridViewCellPaintingEventArgs e)
    {
        if(e.RowIndex<0||e.ColumnIndex!=5||_nativeReservations.Rows[e.RowIndex].Cells[4].Tag is not AiEntry)return;
        e.PaintBackground(e.ClipBounds,true);var state=e.Graphics!.Save();
        var bounds=e.CellBounds; e.Graphics.SetClip(Rectangle.Intersect(e.ClipBounds,bounds));
        // Shear the rendered Japanese glyphs as well as selecting an italic font.
        using var matrix=new System.Drawing.Drawing2D.Matrix(1,0,-0.19f,1,bounds.Left+6+bounds.Height*0.19f,bounds.Top+e.CellStyle!.Padding.Top);
        e.Graphics.Transform=matrix;using var brush=new SolidBrush(Ink);
        e.Graphics.DrawString(Convert.ToString(e.FormattedValue),_nativeItalic!,brush,new RectangleF(0,0,bounds.Width-14,bounds.Height),StringFormat.GenericTypographic);e.Graphics.Restore(state);e.Paint(e.ClipBounds,DataGridViewPaintParts.Border);e.Handled=true;
    }
    void SearchNativeKeyword(){if(_nativeKeywords.CurrentRow?.Tag is not KeywordEntry k)return;_nativeQuery.Text=k.Title;_tabs.SelectedIndex=1;RenderNativeGuide();}
    internal static bool MatchesQuery(Show show,string query)
    {
        query=Data.Clean(query);if(query.Length==0)return true;var m=Regex.Match(query,@"^(.*?)\s+not\s+(.+)$",RegexOptions.IgnoreCase);string yes=m.Success?m.Groups[1].Value:query;
        bool positive=yes.Split(' ',StringSplitOptions.RemoveEmptyEntries).All(p=>Regex.IsMatch(Data.Clean(show.Title),p,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(80)));
        return positive&&(!m.Success||!Regex.IsMatch(Data.Clean(show.Title),m.Groups[2].Value,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(80)));
    }
    void LoadNativeGuideCache()
    {
        string cache=Path.Combine(RecommendationHome,"guide-cache-v3.json");
        try {
            if(File.Exists(cache)){var saved=JsonSerializer.Deserialize<GuideCache>(File.ReadAllText(cache),NativeJson);if(saved!=null){_nativeShows=saved.Shows;_nativeGuideStamp=saved.Fetched.ToLocalTime().ToString("MM/dd HH:mm");if(saved.Fetched.AddHours(6)>DateTimeOffset.Now)_nextGuideAttempt=saved.Fetched.AddHours(6);}}
            else {
                string history=Path.Combine(RecommendationHome,"recommend-history");var dirs=Directory.Exists(history)?Directory.GetDirectories(history).OrderDescending().ToArray():[];
                var folder=dirs.FirstOrDefault(d=>Enumerable.Range(0,32).All(i=>File.Exists(Path.Combine(d,$"guide-{i*6}.html"))));
                if(folder!=null){var stamp=DateTime.ParseExact(Path.GetFileName(folder),"yyyyMMdd-HHmmss",null);var day=new DateTimeOffset(stamp.AddHours(-4).Date,TimeSpan.FromHours(9));var stations=Data.Tuners(Path.Combine(RockFolder,"dtv.ini")).Stations;
                    _nativeShows=Enumerable.Range(0,32).SelectMany(i=>Data.Guide(File.ReadAllText(Path.Combine(folder,$"guide-{i*6}.html")),i*6,day,stations)).GroupBy(s=>s.Key).Select(g=>g.OrderByDescending(s=>s.End).First()).ToList();
                    _nativeGuideStamp=stamp.ToString("MM/dd HH:mm");SaveNativeCache(new DateTimeOffset(stamp,TimeSpan.FromHours(9)));
                }
            }
            foreach(string station in _nativeShows.Select(s=>s.Station).Distinct())if(!_nativeStation.Items.Contains(station))_nativeStation.Items.Add(station);RenderNativeGuide();
        }catch(Exception e){_nativeStatus.Text="保存済み番組表を読み込めません: "+e.Message;}
    }
    void SaveNativeCache(DateTimeOffset fetched){Directory.CreateDirectory(RecommendationHome);var path=Path.Combine(RecommendationHome,"guide-cache-v3.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(new GuideCache(fetched,_nativeShows),NativeJson));File.Move(path+".tmp",path,true);}
    void RenderNativeGuide()
    {
        if(_nativeGuide.Columns.Count==0)return;
        try{
            var matches=_nativeShows.Where(s=>(_includePast.Checked||s.End>DateTimeOffset.Now)&&(_nativeStation.SelectedIndex<=0||s.Station==Convert.ToString(_nativeStation.SelectedItem))&&MatchesQuery(s,_nativeQuery.Text)).OrderBy(s=>s.Start).ToList();
            _nativeGuide.Rows.Clear();foreach(var s in matches){bool booked=_nativeBookings.Any(b=>b.Enabled&&Planner.SameBroadcast(s,b));int i=_nativeGuide.Rows.Add(s.Start.LocalDateTime,s.End.ToLocalTime().ToString("HH:mm"),s.Station,booked?"予約済":"",s.Title);var row=_nativeGuide.Rows[i];row.Tag=s;row.Cells[0].Style.Format="MM/dd(ddd) HH:mm";row.Cells[4].ToolTipText=s.Description;row.Cells[2].Style.ForeColor=StationColor(s.Station);}
            ApplyDatePartitions(_nativeGuide);RenderSchedule();
            _nativeStatus.Text=$"{matches.Count}件 / 保存済み番組表 {_nativeShows.Count}件・取得 {_nativeGuideStamp}　検索は再放送マークも対象";
        }catch(ArgumentException){_nativeStatus.Text="検索式が正しくありません。括弧などを確認してください。";}catch(RegexMatchTimeoutException){_nativeStatus.Text="検索式が複雑すぎます。短い式で検索してください。";}
    }
    internal static async Task<string> NativeGet(string relative)
    {
        var uri=new Uri(Zoo.RuntimeConfiguration.BaseUri,relative);if(!Zoo.RuntimeConfiguration.IsAllowed(uri))throw new InvalidOperationException("接続先がTVRockではありません。");
        return await TvRockHttp.Get(uri);
    }
    async Task RefreshNativeGuide()
    {
        if(_guideRefreshing)return;_guideRefreshing=true;
        try{var now=DateTimeOffset.Now;var day=new DateTimeOffset(now.AddHours(-4).Date,now.Offset);var stations=Data.Tuners(Path.Combine(RockFolder,"dtv.ini")).Stations;var shows=new List<Show>();
            for(int offset=0;offset<192;offset+=6){_nativeStatus.Text=$"番組表を取得中 {offset/6+1}/32（表示・検索は引き続き使えます）";string html=await NativeGet($"day?t={offset}&j=-1&k=-1&v=0");shows.AddRange(Data.Guide(html,offset,day,stations));await Task.Delay(150);}
            if(shows.Count<20)throw new InvalidDataException("番組表の取得件数が足りません。");_nativeShows=shows.GroupBy(s=>s.Key).Select(g=>g.OrderByDescending(s=>s.End).First()).ToList();SaveNativeCache(now);_nativeGuideStamp=now.ToString("MM/dd HH:mm");_nextGuideAttempt=now.AddHours(6);LoadNativeGuideCache();
        }catch(Exception e){_nativeStatus.Text="TVRockに接続できません。保存済み番組表で利用できます。 "+e.Message;}finally{_guideRefreshing=false;}
    }
    async Task OpenNativeLegacy(string relative)
    {
        try{_tabs.SelectedTab=_tabs.TabPages.Cast<TabPage>().First(p=>p.Controls.Contains(_webBrowser));await _webBrowser.EnsureCoreWebView2Async();_detailLanding.Visible=false;_webBrowser.Visible=true;_webBrowser.BringToFront();_webBrowser.CoreWebView2.Navigate(new Uri(Zoo.RuntimeConfiguration.BaseUri,relative).AbsoluteUri);}catch(Exception e){_detailLanding.Visible=true;MessageBox.Show(this,e.Message,"詳細設定");}
    }
    async Task NativeEditBooking()
    {
        if(_nativeReservations.CurrentRow?.Tag is not Booking b)return;
        await OpenNativeLegacy("reg?i="+Uri.EscapeDataString(b.Id));
    }
    async Task NativeBookShow()
    {
        Show? s=_schedule.Visible?_schedule.Selected:_nativeGuide.CurrentRow?.Tag as Show;if(s==null){MessageBox.Show(this,"予約する番組をクリックして選択してください。");return;}
        if(s.Start<DateTimeOffset.Now.AddMinutes(2)){MessageBox.Show(this,"開始済み、または開始直前の番組です。番組表を更新してください。");return;}
        var bookings=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch"));if(bookings.Any(b=>b.Enabled&&Planner.SameBroadcast(s,b))){MessageBox.Show(this,"この放送は予約済みです。");return;}
        var tuners=Data.Tuners(Path.Combine(RockFolder,"dtv.ini")).Tuners;int? device=Planner.FreeDevice(s,tuners,bookings.Where(b=>b.Enabled).ToList(),[]);
        if(device==null){MessageBox.Show(this,"空きチューナーがありません。");return;}
        if(!s.Actions.TryGetValue(device.Value,out string? action)){MessageBox.Show(this,"予約リンクがありません。番組表を更新してください。");return;}
        try{
            // Verify the live guide's event and time before using a cached action.
            int offset=(int)Math.Floor((s.Start-new DateTimeOffset(DateTimeOffset.Now.AddHours(-4).Date,DateTimeOffset.Now.Offset).AddHours(4)).TotalHours/6)*6;
            if(offset<0||offset>=192)throw new InvalidOperationException("番組表を更新してください。");
            string live=await NativeGet($"day?t={offset}&j=-1&k=-1&v=0");var current=Data.Guide(live,offset,new DateTimeOffset(DateTimeOffset.Now.AddHours(-4).Date,DateTimeOffset.Now.Offset),Data.Tuners(Path.Combine(RockFolder,"dtv.ini")).Stations).SingleOrDefault(x=>x.Key==s.Key&&Data.Norm(x.Title)==Data.Norm(s.Title));
            if(current==null||!current.Actions.TryGetValue(device.Value,out action))throw new InvalidOperationException("放送内容が変わりました。番組表を更新してください。");
            var q=Data.Query(action);if(q.GetValueOrDefault("c")!=s.ServiceId.ToString()||q.GetValueOrDefault("e")!=s.EventId.ToString()||q.GetValueOrDefault("d")!=device.ToString()||q.GetValueOrDefault("r")!="1")throw new InvalidDataException("予約リンクの照合に失敗しました。");
            bookings=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch"));if(bookings.Any(b=>b.Enabled&&Planner.SameBroadcast(s,b))||Planner.FreeDevice(s,tuners,bookings.Where(b=>b.Enabled).ToList(),[])!=device)throw new InvalidOperationException("予約状況が変わりました。一覧を更新してください。");
            string backup=Path.Combine(RecommendationHome,"manual-history",DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(backup);File.Copy(Path.Combine(RockFolder,"tvrock.sch"),Path.Combine(backup,"tvrock-before.sch"));
            await NativeGet(action);await Task.Delay(500);var after=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch"));if(!after.Any(b=>b.Enabled&&Planner.SameBroadcast(s,b)))throw new InvalidOperationException("反映を確認できません。再試行する前に予約一覧をご確認ください。");LoadNativeReservations();RenderNativeGuide();MessageBox.Show(this,"予約しました。",s.Title);
        }catch(Exception e){MessageBox.Show(this,e.Message,"予約を完了できませんでした");}
    }
}
