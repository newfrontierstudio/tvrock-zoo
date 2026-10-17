using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    readonly ScheduleGrid _schedule = new();
    readonly DateTimePicker _scheduleDate = new(){Format=DateTimePickerFormat.Custom,CustomFormat="MM月dd日 (ddd)",Width=195};

    readonly CheckBox _showGuideList = new(){Text="一覧表示",AutoSize=true};
    readonly Panel _detailLanding=new(){Dock=DockStyle.Fill,BackColor=Color.White};
    void InitializeScheduleViews(TabPage guidePage)
    {
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(4)};
        var prev=new Button{Text="前日",AutoSize=true};var next=new Button{Text="翌日",AutoSize=true};
        var now=new Button{Text="現在時刻へ",AutoSize=true};now.Click+=(_,_)=>{_scheduleDate.Value=DateTime.Today;_schedule.AutoScrollPosition=new Point(0,(int)DateTime.Now.TimeOfDay.TotalMinutes*3);};
        bar.Controls.AddRange([prev,_scheduleDate,next,now,_showGuideList]);
        var searchbar=guidePage.Controls.OfType<FlowLayoutPanel>().First(p=>p.Controls.Contains(_nativeQuery));var statusbar=guidePage.Controls.OfType<FlowLayoutPanel>().First(p=>p.Controls.Contains(_nativeStatus));
        guidePage.Controls.Clear();var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        searchbar.Dock=DockStyle.Fill;bar.Dock=DockStyle.Fill;statusbar.Dock=DockStyle.Fill;var surface=new Panel{Dock=DockStyle.Fill};surface.Controls.Add(_nativeGuide);surface.Controls.Add(_schedule);layout.Controls.Add(searchbar,0,0);layout.Controls.Add(bar,0,1);layout.Controls.Add(surface,0,2);layout.Controls.Add(statusbar,0,3);guidePage.Controls.Add(layout);
        prev.Click+=(_,_)=>_scheduleDate.Value=_scheduleDate.Value.AddDays(-1);next.Click+=(_,_)=>_scheduleDate.Value=_scheduleDate.Value.AddDays(1);
        _scheduleDate.ValueChanged+=(_,_)=>RenderSchedule();_showGuideList.CheckedChanged+=(_,_)=>RenderSchedule();
        _schedule.StationInk=StationColor;
        _schedule.SelectShow=s=>_nativeStatus.Text=s.Start.ToLocalTime().ToString("MM/dd HH:mm")+" "+s.Station+" "+s.Title;
        _schedule.HoverShow=(s,p)=>{if(s==null)HideLargeTip();else QueueLargeTip(_schedule,string.IsNullOrWhiteSpace(s.Description)?"番組説明が未取得です。番組表を更新してください。":s.Description,p);};
        _nativeReservations.Tag="date-partitions";_nativeGuide.Tag="date-partitions";
        _nativeReservations.RowPostPaint+=PaintDatePartition;_nativeGuide.RowPostPaint+=PaintDatePartition;
        _nativeKeywords.CellPainting+=PaintKeywordStations;
        var detailPage=_tabs.TabPages.Cast<TabPage>().First(p=>p.Controls.Contains(_webBrowser));detailPage.Controls.Add(_detailLanding);_webBrowser.Visible=false;_detailLanding.BringToFront();
        var content=new FlowLayoutPanel{Dock=DockStyle.Top,Height=280,AutoSize=false,FlowDirection=FlowDirection.TopDown,Padding=new Padding(24)};
        var title=new Label{Text="詳細設定",AutoSize=true,Font=new Font(Font.FontFamily,14,FontStyle.Bold)};
        var explanation=new Label{Text="予約の詳細編集・TVRockの設定を、この画面から開けます。",AutoSize=true,Margin=new Padding(0,12,0,12)};
        var open=new Button{Text="TVRockの設定画面を開く",AutoSize=true};var state=new Label{AutoSize=true,Text="おすすめは3日ごと・最大20件。自動実行は上部のON／OFFで切り替えます。",Margin=new Padding(0,15,0,0)};
        content.Controls.AddRange([title,explanation,open,state]);_detailLanding.Controls.Add(content);StyleControls(content);
        open.Click+=async(_,_)=>{_detailLanding.Visible=false;await OpenNativeLegacy("kwl");};
        var connection=new Label{Dock=DockStyle.Bottom,Height=30,Text="",ForeColor=Color.FromArgb(120,52,25)};detailPage.Controls.Add(connection);
        _webBrowser.NavigationStarting+=(_,_)=>{connection.Text="TVRockを読み込んでいます…";};
        _webBrowser.NavigationCompleted+=(_,e)=>{connection.Text=e.IsSuccess?"":"TVRockへ接続できません。設定画面を開くボタンで再試行できます。";if(!e.IsSuccess){_detailLanding.Visible=true;_webBrowser.Visible=false;}};
        _tabs.SelectedIndexChanged+=(_,_)=>{if(_tabs.SelectedTab==detailPage&&_webBrowser.Source==null)_detailLanding.Visible=true;};
        RenderSchedule();
    }
    void RenderSchedule()
    {
        bool list=_showGuideList.Checked||!string.IsNullOrWhiteSpace(_nativeQuery.Text);_nativeGuide.Visible=list;_schedule.Visible=!list;
        if(_schedule.Day!=_scheduleDate.Value.Date)_schedule.AutoScrollPosition=Point.Empty;_schedule.Day=_scheduleDate.Value.Date;
        _schedule.Shows=_nativeShows.Where(s=>_nativeStation.SelectedIndex<=0||s.Station==Convert.ToString(_nativeStation.SelectedItem)).ToList();
        _schedule.Booked=_nativeShows.Where(s=>_nativeBookings.Any(b=>b.Enabled&&Planner.SameBroadcast(s,b))).Select(s=>s.Key).ToHashSet();_schedule.RefreshSchedule();
    }
    static void ApplyDatePartitions(DataGridView grid)
    {
        if(Convert.ToString(grid.Tag)!="date-partitions")return;int dateColumn=grid.Columns["start"].Index;
        bool enabled=!NativeSort.TryGetValue(grid,out var sort)||sort.Column==dateColumn;DateTime? previous=null;
        foreach(DataGridViewRow row in grid.Rows){var date=row.Cells[dateColumn].Value is DateTime value?value.Date:(DateTime?)null;bool boundary=enabled&&previous.HasValue&&date!=previous;row.Cells[dateColumn].Tag=boundary?"date-boundary":null;row.DefaultCellStyle.Padding=new Padding(6,2,6,2);row.Height=grid.DefaultCellStyle.Font!.Height+9;previous=date;}
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
        foreach(string station in Convert.ToString(e.FormattedValue)!.Split(" / ")){var size=TextRenderer.MeasureText(station,e.CellStyle!.Font);if(x+size.Width>e.CellBounds.Right-4)break;TextRenderer.DrawText(e.Graphics,station,e.CellStyle.Font,new Rectangle(x,e.CellBounds.Top,size.Width,e.CellBounds.Height),StationColor(station),TextFormatFlags.VerticalCenter|TextFormatFlags.Left);x+=size.Width+8;}
        e.Paint(e.ClipBounds,DataGridViewPaintParts.Border);e.Handled=true;
    }
}
