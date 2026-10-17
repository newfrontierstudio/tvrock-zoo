using System.Text.Json;
using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    readonly DataGridView _disabledReservations=NativeGrid();
    readonly Panel _disabledBox=new(){Text="無効予約 — ×を押すと復活",Dock=DockStyle.Bottom,Height=180};
    bool _changingReservation;
    void InitializeReservationSwitches(TabPage page)
    {
        _includeDisabled.Visible=false;
        InitializeReservationPresentation();
        InstallReservationHover(_nativeReservations,false);InstallReservationHover(_disabledReservations,true);
        foreach(DataGridViewColumn c in _nativeReservations.Columns)_disabledReservations.Columns.Add((DataGridViewColumn)c.Clone());
        _disabledReservations.DefaultCellStyle=_nativeReservations.DefaultCellStyle.Clone();_disabledReservations.ColumnHeadersDefaultCellStyle=_nativeReservations.ColumnHeadersDefaultCellStyle.Clone();_disabledReservations.EnableHeadersVisualStyles=false;
        _disabledReservations.RowTemplate.Height=_nativeReservations.RowTemplate.Height;_disabledBox.Controls.Add(_disabledReservations);
        var toolbar=page.Controls.OfType<FlowLayoutPanel>().First();toolbar.Visible=false;page.Controls.Clear();
        _reservationSplit.Panel1.Controls.Add(_nativeReservations);_reservationSplit.Panel2.Controls.Add(_disabledBox);_disabledBox.Dock=DockStyle.Fill;page.Controls.Add(_reservationSplit);
        _reservationSplit.SplitterMoved+=(_,_)=>{if(!_applyingReservationSplit&&!_disabledCollapsed){_disabledExpandedHeight=_reservationSplit.Height-_reservationSplit.SplitterDistance-_reservationSplit.SplitterWidth;QueueLayoutSave();}};
        _reservationSplit.SplitterMoving+=(_,_)=>{if(_disabledCollapsed){_disabledCollapsed=false;_disabledReservations.Visible=true;UpdateDisabledCaption();}};
        _reservationSplit.Resize+=(_,_)=>ApplyReservationSplit();
        _nativeReservations.CellClick+=async(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex==0&&_nativeReservations.Rows[e.RowIndex].Tag is Booking b)await ChangeReservationEnabled(b,false);};
        _disabledReservations.CellClick+=async(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex==0&&_disabledReservations.Rows[e.RowIndex].Tag is Booking b)await ChangeReservationEnabled(b,true);};
        _nativeReservations.Columns[0].ToolTipText="○をクリック：録画しない一覧へ";_disabledReservations.Columns[0].ToolTipText="×をクリック：有効な予約に戻す";
        _nativeReservations.Columns[0].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;_disabledReservations.Columns[0].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
    }
    void InstallReservationHover(DataGridView grid,bool restore)
    {
        int hoverRow=-1;
        void Reset(){int old=hoverRow;hoverRow=-1;grid.Cursor=Cursors.Default;if(old>=0&&old<grid.Rows.Count)grid.InvalidateCell(0,old);}
        grid.CellMouseMove+=(_,e)=>{int next=e.ColumnIndex==0&&e.RowIndex>=0&&grid.Rows[e.RowIndex].Tag is Booking?e.RowIndex:-1;if(next==hoverRow)return;Reset();hoverRow=next;if(next>=0){grid.Cursor=Cursors.Hand;grid.InvalidateCell(0,next);HideLargeTip();QueueLargeTip(grid,restore?"クリックで録画する予約に戻す":"クリックで録画しない一覧へ移動",grid.PointToClient(Cursor.Position));}};
        grid.CellMouseLeave+=(_,_)=>Reset();grid.MouseLeave+=(_,_)=>Reset();grid.Scroll+=(_,_)=>Reset();grid.RowsRemoved+=(_,_)=>Reset();
        grid.CellPainting+=(_,e)=>{
            if(e.RowIndex<0||e.ColumnIndex!=0||e.RowIndex!=hoverRow)return;
            e.Paint(e.ClipBounds,DataGridViewPaintParts.All);var bounds=Rectangle.Inflate(e.CellBounds,-4,-3);
            using var fill=new SolidBrush(Color.FromArgb(225,239,253));using var border=new Pen(Color.FromArgb(77,132,185));
            e.Graphics!.FillRectangle(fill,bounds);e.Graphics.DrawRectangle(border,bounds);
            using var font=new Font(e.CellStyle!.Font??grid.Font,FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics,restore?"×":"○",font,bounds,Color.FromArgb(22,77,132),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);e.Handled=true;
        };
    }
    void LoadDisabledReservations()
    {
        if(_disabledReservations.Columns.Count==0)return;_disabledReservations.Rows.Clear();
        foreach(var b in _nativeBookings.Where(b=>!b.Enabled&&(_includeFinishedBookings.Checked||b.End>DateTimeOffset.Now)).OrderBy(b=>b.Start)){
            int i=_disabledReservations.Rows.Add("×",b.Start.LocalDateTime,(int)Math.Round((b.End-b.Start).TotalMinutes),b.Station,"",b.Title);var row=_disabledReservations.Rows[i];row.Tag=b;row.Cells[1].Style.Format="MM/dd(ddd) HH:mm";row.Cells[2].Style.Format="0分";row.Cells[3].Style.ForeColor=StationColor(b.Station);
        }
        UpdateDisabledCaption();
    }
    void RevealChangedReservation(string id,bool enabled)
    {
        if(!enabled)SetDisabledCollapsed(false);
        var grid=enabled?_nativeReservations:_disabledReservations;
        var row=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>r.Tag is Booking b&&b.Id==id);
        if(row==null)return;
        grid.ClearSelection();row.Selected=true;grid.CurrentCell=row.Cells["title"];grid.Parent?.PerformLayout();
        if(grid.Visible&&grid.DisplayedRowCount(false)>0)grid.FirstDisplayedScrollingRowIndex=row.Index;
    }
    async Task ChangeReservationEnabled(Booking booking,bool enable)
    {
        if(_changingReservation)return;_changingReservation=true;
        try{
            var current=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch")).SingleOrDefault(b=>b.Id==booking.Id&&b.Start==booking.Start&&Data.Norm(b.Title)==Data.Norm(booking.Title));if(current==null)throw new IOException("予約が更新されています。一覧を更新してください。");if(current.Enabled==enable){LoadNativeReservations();RevealChangedReservation(current.Id,enable);return;}
            string folder=Path.Combine(RecommendationHome,"reservation-changes",DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(folder);File.Copy(Path.Combine(RockFolder,"tvrock.sch"),Path.Combine(folder,"before.sch"));
            string html=await NativeGet("reg?i="+current.Id);File.WriteAllText(Path.Combine(folder,"before.html"),html);var fields=Data.Form(html,"予約を変更");if(fields.GetValueOrDefault("i")!=current.Id||Data.Norm(fields.GetValueOrDefault("title",""))!=Data.Norm(current.Title))throw new InvalidDataException("予約フォームの照合に失敗しました。");
            if(enable)fields["valid"]="true";else fields.Remove("valid");
            Exception? sendError=null;
            try{await NativeGet("regsch?"+string.Join("&",fields.Select(p=>Data.Encode(p.Key)+"="+Data.Encode(p.Value))));}
            catch(Exception e)when(e is HttpRequestException or TaskCanceledException){sendError=e;File.WriteAllText(Path.Combine(folder,"transport-error.txt"),e.ToString());}
            Booking? updated=null;
            for(int attempt=0;attempt<12;attempt++){await Task.Delay(250);updated=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch")).SingleOrDefault(b=>b.Id==current.Id&&b.Start==current.Start&&Data.Norm(b.Title)==Data.Norm(current.Title));if(updated?.Enabled==enable)break;}if(updated==null||updated.Enabled!=enable)throw new IOException(sendError==null?"反映を確認できません。一覧を更新して状態をご確認ください。":"TVRockとの通信が途切れ、変更を確認できませんでした。状態を更新しました。予約変更は自動再送していません。",sendError);
            RecordReservationChange(current,enable);
            File.WriteAllText(Path.Combine(folder,"result.txt"),current.Id+" enabled="+enable);LoadNativeReservations();RevealChangedReservation(current.Id,enable);
        }catch(Exception e){Log("予約の切替: "+e);LoadNativeReservations();MessageBox.Show(this,e is HttpRequestException or TaskCanceledException?"TVRockの編集画面を読み込めませんでした。少し待ってから、もう一度クリックしてください。":e.Message,"予約の切替");}finally{_changingReservation=false;}
    }
}
