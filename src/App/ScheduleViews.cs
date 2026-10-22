using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    readonly ScheduleGrid _schedule = new();
    readonly DateTimePicker _scheduleDate = new(){Format=DateTimePickerFormat.Custom,CustomFormat="MM月dd日 (ddd)",Width=195};

    readonly CheckBox _showGuideList = new(){Text="一覧表示",AutoSize=true};
    readonly Panel _detailLanding=new(){Dock=DockStyle.Fill,BackColor=Color.White};
    readonly ContextMenuStrip _guideContextMenu=new();
    void ShowGuideContext(Show show,Control owner,Point location){HideLargeTip();var menu=_guideContextMenu;menu.Items.Clear();bool booked=_nativeBookings.Any(b=>b.Enabled&&Planner.SameBroadcast(show,b));var action=menu.Items.Add(booked?"予約済み":"予約");action.Enabled=!booked&&show.End>DateTimeOffset.Now;action.Click+=async(_,_)=>await NativeBookShow(show);menu.Show(owner,location);}
    void InitializeScheduleViews(TabPage guidePage)
    {
        InitializeBackGesture();
        guidePage.Disposed+=(_,_)=>_guideContextMenu.Dispose();
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(4)};
        var prev=new Button{Text="前日",AutoSize=true};var next=new Button{Text="翌日",AutoSize=true};
        var now=new Button{Text="現在時刻へ",AutoSize=true};now.Click+=(_,_)=>{ShowCurrentGuideTime();};
        var refresh=new Button{Text="最新の情報（F5）",AutoSize=true};refresh.Click+=(_,_)=>RefreshStudio();bar.Controls.AddRange([prev,_scheduleDate,next,now,_showGuideList,refresh]);var stationOrder=new Button{Text="局の並び順",AutoSize=true};bar.Controls.Add(stationOrder);stationOrder.Click+=(_,_)=>EditStationOrder();
        var searchbar=guidePage.Controls.OfType<FlowLayoutPanel>().First(p=>p.Controls.Contains(_nativeQuery));var statusbar=guidePage.Controls.OfType<FlowLayoutPanel>().First(p=>p.Controls.Contains(_nativeStatus));
        guidePage.Controls.Clear();var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        searchbar.Dock=DockStyle.Fill;bar.Dock=DockStyle.Fill;statusbar.Dock=DockStyle.Fill;var surface=new Panel{Dock=DockStyle.Fill};surface.Controls.Add(_nativeGuide);surface.Controls.Add(_schedule);layout.Controls.Add(searchbar,0,0);layout.Controls.Add(bar,0,1);layout.Controls.Add(surface,0,2);layout.Controls.Add(statusbar,0,3);guidePage.Controls.Add(layout);
        prev.Click+=(_,_)=>_scheduleDate.Value=_scheduleDate.Value.AddDays(-1);next.Click+=(_,_)=>_scheduleDate.Value=_scheduleDate.Value.AddDays(1);
        _scheduleDate.ValueChanged+=(_,_)=>RenderSchedule();_showGuideList.CheckedChanged+=(_,_)=>RenderSchedule();
        _schedule.StationInk=StationColor;
        _schedule.BookShow=async s=>await NativeBookShow(s);_schedule.StationOrderChanged=order=>{try{Directory.CreateDirectory(RecommendationHome);File.WriteAllLines(Path.Combine(RecommendationHome,"station-order.txt"),order);}catch(Exception error){MessageBox.Show(this,error.Message,"局の並び順を保存できません");}};guidePage.Enter+=(_,_)=>{if(IsHandleCreated)BeginInvoke((Action)ShowCurrentGuideTime);};
        _schedule.ContextShow=(s,p)=>ShowGuideContext(s,_schedule,p);_nativeGuide.CellMouseClick+=(_,e)=>{if(e.Button==MouseButtons.Right&&e.RowIndex>=0&&_nativeGuide.Rows[e.RowIndex].Tag is Show s)ShowGuideContext(s,_nativeGuide,_nativeGuide.PointToClient(Cursor.Position));};
        _schedule.SelectShow=s=>_nativeStatus.Text=s.Start.ToLocalTime().ToString("MM/dd HH:mm")+" "+s.Station+" "+s.Title;
        InitializeGuideAssignments();_schedule.HoverShow=(s,p)=>{if(s==null)HideLargeTip();else QueueLargeTip(_schedule,GuidePopupWithSearchWord(s)+AssignmentHover(s),p);};
        _nativeReservations.Tag="date-partitions";_nativeGuide.Tag="date-partitions";
        _nativeReservations.RowPostPaint+=PaintDatePartition;_nativeGuide.RowPostPaint+=PaintDatePartition;
        _nativeKeywords.CellPainting+=PaintKeywordStations;
        var detailPage=_tabs.TabPages.Cast<TabPage>().First(p=>p.Controls.Contains(_webBrowser));detailPage.Controls.Add(_detailLanding);_webBrowser.Visible=false;_detailLanding.BringToFront();
        var content=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=false,FlowDirection=FlowDirection.TopDown,Padding=new Padding(24)};
        var title=new Label{Text="詳細設定",AutoSize=true,Font=new Font(Font.FontFamily,14,FontStyle.Bold)};
        var explanation=new Label{Text="予約の詳細編集・TVRockの設定を、この画面から開けます。",AutoSize=true,Margin=new Padding(0,12,0,12)};
        var open=new Button{Text="TVRockのWeb画面を開く",AutoSize=true};var state=new Label{AutoSize=true,Text="おすすめは3日ごと・最大20件。自動実行は上部のON／OFFで切り替えます。",Margin=new Padding(0,15,0,0)};
        var tunerCheck=new Button{Text="接続・チューナーを確認",AutoSize=true};tunerCheck.Click+=async(_,_)=>await ShowTunerDiagnostics();
        content.Controls.AddRange([title,explanation,open,tunerCheck,state]);InitializeGestureOptions(content);_detailLanding.AutoScroll=true;_detailLanding.Controls.Add(content);StyleControls(content);
        open.Click+=async(_,_)=>{_detailLanding.Visible=false;await OpenNativeLegacy("kwl");};
        var connection=new Label{Dock=DockStyle.Bottom,Height=30,Text="",ForeColor=Color.FromArgb(120,52,25)};detailPage.Controls.Add(connection);
        _webBrowser.NavigationStarting+=(_,_)=>{connection.Text="TVRockを読み込んでいます…";};
        _webBrowser.NavigationCompleted+=(_,e)=>{connection.Text=e.IsSuccess?"":"TVRockへ接続できません。設定画面を開くボタンで再試行できます。";if(!e.IsSuccess){_detailLanding.Visible=true;_webBrowser.Visible=false;}};
        _tabs.SelectedIndexChanged+=(_,_)=>{if(_tabs.SelectedTab==detailPage&&_webBrowser.Source==null)_detailLanding.Visible=true;};
        RenderSchedule();
    }
    internal static string GuidePopupText(Show show)=>$"{show.Station}  {show.Start:MM/dd HH:mm} ～ {show.End:HH:mm}\\n{show.Title}".Replace("\\n",Environment.NewLine)+(string.IsNullOrWhiteSpace(show.Description)?"":Environment.NewLine+Environment.NewLine+show.Description);
    void ShowCurrentGuideTime(){_scheduleDate.Value=DateTime.Today;RenderSchedule();_schedule.AutoScrollPosition=new Point(0,Math.Max(0,ScheduleGrid.TimelineY(DateTime.Now.TimeOfDay.TotalMinutes)-45));}
    void RenderSchedule()
    {
        bool list=_showGuideList.Checked||!string.IsNullOrWhiteSpace(_nativeQuery.Text)||!string.IsNullOrWhiteSpace(_guideNgWords.Text);_nativeGuide.Visible=list;_schedule.Visible=!list;
        if(_schedule.Day!=_scheduleDate.Value.Date)_schedule.AutoScrollPosition=Point.Empty;_schedule.Day=_scheduleDate.Value.Date;
        _schedule.Shows=_nativeShows.Where(s=>_nativeStation.SelectedIndex<=0||s.Station==Convert.ToString(_nativeStation.SelectedItem)).ToList();
        var index=new GuideBookingIndex(_nativeBookings);var booked=new HashSet<string>();var disabled=new HashSet<string>();var assignments=new Dictionary<string,GuideAssignment>();
        foreach(var show in _nativeShows)foreach(var booking in index.Matches(show)){if(booking.Enabled){booked.Add(show.Key);if(!assignments.ContainsKey(show.Key))assignments[show.Key]=AssignmentFor(booking);}else disabled.Add(show.Key);}
        _schedule.Assignments=assignments;_schedule.StationOrder=OrderedStations().ToList();_schedule.Disabled=disabled;_schedule.Booked=booked;_schedule.RefreshSchedule();
    }
    static void ApplyDatePartitions(DataGridView grid)
    {
        if(Convert.ToString(grid.Tag)!="date-partitions")return;int dateColumn=grid.Columns["start"].Index;
        bool enabled=!NativeSort.TryGetValue(grid,out var sort)||sort.Column==dateColumn;DateTime? previous=null;
        foreach(DataGridViewRow row in grid.Rows){var date=row.Cells[dateColumn].Value is DateTime value?value.Date:(DateTime?)null;bool boundary=enabled&&previous.HasValue&&date!=previous;row.Cells[dateColumn].Tag=boundary?"date-boundary":null;row.DefaultCellStyle.Padding=new Padding(6,0,6,0);row.Height=grid.DefaultCellStyle.Font!.Height+5;previous=date;}
    }
    static void PaintDatePartition(object? sender,DataGridViewRowPostPaintEventArgs e)
    {
        var grid=(DataGridView)sender!;int col=grid.Columns["start"].Index;if(Convert.ToString(grid.Rows[e.RowIndex].Cells[col].Tag)!="date-boundary")return;
        using var pen=new Pen(Color.FromArgb(73,99,133),1.5f);e.Graphics.DrawLine(pen,e.RowBounds.Left,e.RowBounds.Top+1,e.RowBounds.Right,e.RowBounds.Top+1);
    }
    void PaintKeywordStations(object? sender,DataGridViewCellPaintingEventArgs e)
    {
        if(e.RowIndex<0||e.ColumnIndex!=4)return;e.PaintBackground(e.ClipBounds,true);int x=e.CellBounds.Left+6;
        if(Convert.ToString(e.FormattedValue)!.Split(" / ").Length>=6){TextRenderer.DrawText(e.Graphics!,"全局",e.CellStyle!.Font,e.CellBounds,Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);e.Handled=true;return;}
        var state=e.Graphics!.Save();e.Graphics.SetClip(e.ClipBounds);try{foreach(string station in Convert.ToString(e.FormattedValue)!.Split(" / ")){if(x>=e.CellBounds.Right-10)break;int width=StationBadgeRenderer.Draw(e.Graphics,station,e.CellStyle!.Font??_nativeKeywords.Font,new Rectangle(x,e.CellBounds.Top+2,e.CellBounds.Right-x-4,e.CellBounds.Height-4));x+=width+4;}}finally{e.Graphics.Restore(state);}
        e.Paint(e.ClipBounds,DataGridViewPaintParts.Border);e.Handled=true;
    }
}
