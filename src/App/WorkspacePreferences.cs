using System.Text.Json;
using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    readonly Button _undoButton=new(){Text="元に戻す",AutoSize=true,Enabled=false};
    readonly Button _foldDisabled=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,FlatStyle=FlatStyle.Flat};
    readonly System.Windows.Forms.Timer _saveLayoutDelay=new(){Interval=600};
    bool _disabledCollapsed,_layoutReady,_undoInProgress;
    readonly SplitContainer _reservationSplit=new(){Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,FixedPanel=FixedPanel.Panel2,SplitterWidth=7,Size=new Size(1000,800),BackColor=Color.FromArgb(214,224,236),AccessibleName="予約一覧と録画しない一覧の境界。上下にドラッグして高さを調整"};
    int _disabledExpandedHeight=180;bool _applyingReservationSplit;
    UndoChange? _undoChange;
    internal static string? TestLayoutFile;
    readonly Dictionary<string,GridLayout> _pendingGridLayouts=[];
    sealed record UndoChange(Booking Before,bool Applied);
    sealed class GridLayout {public Dictionary<string,int> Widths{get;set;}=[];public string? First{get;set;}public int Index{get;set;}public int Horizontal{get;set;}}
    sealed class WorkspaceLayout {public bool Merged{get;set;}public bool Collapsed{get;set;}public int DisabledHeight{get;set;}=180;public int Tab{get;set;}public Dictionary<string,GridLayout> Grids{get;set;}=[];public DateTime Day{get;set;}public Point Scroll{get;set;}}
    IEnumerable<(string Name,DataGridView Grid)> LayoutGrids()=>new[]{("reservations",_nativeReservations),("disabled",_disabledReservations),("keywords",_nativeKeywords),("guide",_nativeGuide)};
    static string? RowIdentity(DataGridViewRow row)=>row.Tag switch{Booking b=>b.Id,KeywordEntry k=>k.Id,Show s=>s.Key,_=>null};
    void InitializeWorkspacePreferences(FlowLayoutPanel toolbar,TabPage page)
    {
        toolbar.Controls.Add(_undoButton);StyleControls(_undoButton);_undoButton.Click+=async(_,_)=>await UndoReservationChange();
        _disabledBox.Text="";_disabledBox.Controls.Clear();var box=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};box.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));box.RowStyles.Add(new RowStyle(SizeType.Absolute,DisabledCaptionHeight));box.RowStyles.Add(new RowStyle(SizeType.Percent,100));box.Controls.Add(_foldDisabled,0,0);box.Controls.Add(_disabledReservations,0,1);_disabledBox.Controls.Add(box);StyleControls(_foldDisabled);_foldDisabled.Padding=new Padding(2);_foldDisabled.MinimumSize=Size.Empty;_foldDisabled.Margin=Padding.Empty;_foldDisabled.AutoSize=false;_foldDisabled.AutoEllipsis=true;
        void FitCaption(){box.RowStyles[0].Height=DisabledCaptionHeight;ApplyReservationSplit();box.PerformLayout();}
        _foldDisabled.FontChanged+=(_,_)=>FitCaption();DpiChanged+=(_,_)=>FitCaption();FitCaption();
        _foldDisabled.Click+=(_,_)=>{SetDisabledCollapsed(!_disabledCollapsed);QueueLayoutSave();};
        foreach(var item in LayoutGrids()){
            var grid=item.Grid;grid.AllowUserToResizeColumns=true;
            grid.CellPainting+=(_,e)=>{if(e.RowIndex!=-1||e.ColumnIndex<0)return;e.Paint(e.ClipBounds,DataGridViewPaintParts.All);using var pen=new Pen(Color.White,2);e.Graphics!.DrawLine(pen,e.CellBounds.Right-1,e.CellBounds.Top+3,e.CellBounds.Right-1,e.CellBounds.Bottom-3);e.Handled=true;};
            grid.ColumnWidthChanged+=(_,_)=>QueueLayoutSave();grid.Scroll+=(_,_)=>QueueLayoutSave();
        }
        _tabs.SelectedIndexChanged+=(_,_)=>{QueueLayoutSave();if(_layoutReady)BeginInvoke(new Action(RestorePendingScroll));};_scheduleDate.ValueChanged+=(_,_)=>QueueLayoutSave();_schedule.ViewChanged+=QueueLayoutSave;
        _saveLayoutDelay.Tick+=(_,_)=>{_saveLayoutDelay.Stop();SaveWorkspaceLayout();};FormClosing+=(_,_)=>SaveWorkspaceLayout();
        Shown+=(_,_)=>BeginInvoke(new Action(()=>{RestoreWorkspaceLayout();_layoutReady=true;}));
        SetDisabledCollapsed(false);
    }
    int DisabledCaptionHeight=>Math.Max((int)Math.Ceiling(32*DeviceDpi/96d),_foldDisabled.Font.Height+(int)Math.Ceiling(12*DeviceDpi/96d));
    void SetDisabledCollapsed(bool collapsed){_disabledCollapsed=collapsed;_disabledReservations.Visible=!collapsed;ApplyReservationSplit();UpdateDisabledCaption();}
    void ApplyReservationSplit()
    {
        if(_reservationSplit.Panel2Collapsed)return;
        int available=_reservationSplit.ClientSize.Height-_reservationSplit.SplitterWidth;if(available<100)return;
        int minimum=DisabledCaptionHeight+(_disabledCollapsed?0:_disabledReservations.ColumnHeadersHeight+24);
        int bottom=Math.Clamp(_disabledCollapsed?DisabledCaptionHeight:_disabledExpandedHeight,Math.Min(minimum,available-60),available-60);
        _applyingReservationSplit=true;try{_reservationSplit.SplitterDistance=available-bottom;}finally{_applyingReservationSplit=false;}
    }
    void UpdateDisabledCaption(){_disabledBox.Text="";_foldDisabled.Text=$"{(_disabledCollapsed?"▸":"▾")} 録画しない {_disabledReservations.Rows.Count}件"+(_disabledCollapsed?" — クリックで開く":" — ×で復活・クリックで折りたたむ");}
    void QueueLayoutSave(){if(!_layoutReady)return;_saveLayoutDelay.Stop();_saveLayoutDelay.Start();}
    static string LayoutFile=>TestLayoutFile??Path.Combine(RecommendationHome,"workspace-layout.json");
    void SaveWorkspaceLayout()
    {
        if(!_layoutReady||(TestLayoutFile==null&&Environment.GetCommandLineArgs().Contains("--self-test")))return;
        try{var state=new WorkspaceLayout{Merged=_mergeReservations,Collapsed=_disabledCollapsed,DisabledHeight=_disabledExpandedHeight,Tab=_tabs.SelectedIndex,Day=_scheduleDate.Value.Date,Scroll=_schedule.AutoScrollPosition};foreach(var item in LayoutGrids()){var grid=item.Grid;int first=grid.FirstDisplayedScrollingRowIndex;state.Grids[item.Name]=new GridLayout{Widths=grid.Columns.Cast<DataGridViewColumn>().Where(c=>c.Visible).ToDictionary(c=>c.Name,c=>c.Width),First=first>=0?RowIdentity(grid.Rows[first]):null,Index=Math.Max(0,first),Horizontal=grid.HorizontalScrollingOffset};}Directory.CreateDirectory(RecommendationHome);File.WriteAllText(LayoutFile+".tmp",JsonSerializer.Serialize(state));File.Move(LayoutFile+".tmp",LayoutFile,true);}catch(IOException){}catch(InvalidOperationException){}
    }
    void RestoreWorkspaceLayout()
    {
        try{if(!File.Exists(LayoutFile))return;var state=JsonSerializer.Deserialize<WorkspaceLayout>(File.ReadAllText(LayoutFile));if(state==null)return;SetReservationView(state.Merged,false);_disabledExpandedHeight=Math.Max(80,state.DisabledHeight);SetDisabledCollapsed(state.Collapsed);if(state.Day.Year>2020)_scheduleDate.Value=state.Day;
            foreach(var item in LayoutGrids()){if(!state.Grids.TryGetValue(item.Name,out var saved))continue;foreach(var size in saved.Widths){if(!item.Grid.Columns.Contains(size.Key))continue;var column=item.Grid.Columns[size.Key];if(item.Name is "reservations" or "disabled" && column.Name=="title")continue;column.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;column.Width=Math.Clamp(size.Value,30,2000);}}
            FitReservationColumns(_nativeReservations);FitReservationColumns(_disabledReservations);
            _pendingGridLayouts.Clear();foreach(var pair in state.Grids)_pendingGridLayouts[pair.Key]=pair.Value;_tabs.SelectedIndex=Math.Clamp(state.Tab,0,_tabs.TabCount-1);BeginInvoke(new Action(()=>{RestorePendingScroll();_schedule.AutoScrollPosition=state.Scroll;}));
        }catch(Exception e)when(e is IOException or JsonException or ArgumentException or InvalidOperationException){Log("表示位置の復元を見送りました: "+e.Message);}
    }
    void RestorePendingScroll(){foreach(var item in LayoutGrids()){if(!_pendingGridLayouts.TryGetValue(item.Name,out var saved)||!item.Grid.Visible||item.Grid.Rows.Count==0||item.Grid.DisplayedRowCount(false)==0)continue;var row=item.Grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>RowIdentity(r)==saved.First);item.Grid.FirstDisplayedScrollingRowIndex=row?.Index??Math.Min(saved.Index,item.Grid.Rows.Count-1);item.Grid.HorizontalScrollingOffset=item.Name is "reservations" or "disabled"?0:Math.Max(0,saved.Horizontal);_pendingGridLayouts.Remove(item.Name);}}
    async Task UndoReservationChange()
    {
        var undo=_undoChange;if(undo==null||_changingReservation)return;var current=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch")).SingleOrDefault(b=>b.Id==undo.Before.Id);if(current==null||current.Enabled!=undo.Applied){MessageBox.Show(this,"その後に予約状態が変わったため、元に戻せません。");_undoChange=null;_undoButton.Enabled=false;return;}
        _undoInProgress=true;try{await ChangeReservationEnabled(undo.Before,undo.Before.Enabled);}finally{_undoInProgress=false;}
    }
    void RecordReservationChange(Booking before,bool applied)
    {
        if(_undoInProgress){_undoChange=null;_undoButton.Enabled=false;}
        else{_undoChange=new(before,applied);_undoButton.Enabled=true;_undoButton.AccessibleDescription=before.Title;}
    }
}
