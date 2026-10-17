using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    bool _fittingReservationColumns;
    readonly Dictionary<DataGridView,Dictionary<string,bool>> _recordingStates=[];
    internal static bool IsRecordingWindow(Booking booking,DateTimeOffset now)=>booking.Enabled&&booking.Start<=now&&booking.End>now;
    internal static DateTimeOffset NextReservationBoundary(IEnumerable<Booking> bookings,DateTimeOffset now)=>bookings.SelectMany(b=>new[]{b.Start,b.End}).Where(time=>time>now).DefaultIfEmpty(DateTimeOffset.MaxValue).Min();
    void InitializeReservationPresentation()
    {
        foreach(var grid in new[]{_nativeReservations,_disabledReservations})
        {
            grid.ScrollBars=ScrollBars.Vertical;
            grid.SizeChanged+=(_,_)=>FitReservationColumns(grid);
            grid.ColumnWidthChanged+=(_,_)=>FitReservationColumns(grid);
            grid.CellFormatting+=(_,e)=>{
                if(e.RowIndex<0||grid.Rows[e.RowIndex].Tag is not Booking b)return;
                if(!b.Enabled){e.CellStyle.BackColor=Color.FromArgb(247,247,247);e.CellStyle.ForeColor=Color.FromArgb(100,108,116);}
                if(IsRecordingWindow(b,DateTimeOffset.Now)){e.CellStyle.BackColor=Color.FromArgb(255,236,236);e.CellStyle.SelectionBackColor=Color.FromArgb(249,215,215);}
            };
            FitReservationColumns(grid);
        }

    }
    internal static string ReservationDisplayFingerprint(IEnumerable<Booking> bookings,IEnumerable<AiEntry> recommendations)=>System.Text.Json.JsonSerializer.Serialize(new{Bookings=bookings.OrderBy(b=>b.Id).Select(b=>new{b.Id,b.Title,b.Station,b.ServiceId,b.EventId,b.Start,b.End,b.Device,b.Enabled,Search=b.Fields.GetValueOrDefault("SEARCHID","")}),Recommendations=recommendations.OrderBy(a=>a.ReservationId)});
    internal int RefreshReservationClock(DateTimeOffset now)
    {
        int changed=0;
        foreach(var grid in new[]{_nativeReservations,_disabledReservations}){
            if(!_recordingStates.TryGetValue(grid,out var states))_recordingStates[grid]=states=[];
            bool removed=false;
            foreach(var row in grid.Rows.Cast<DataGridViewRow>().ToArray()){
                if(row.Tag is not Booking booking)continue;
                if(!_includeFinishedBookings.Checked&&booking.End<=now){states.Remove(booking.Id);grid.Rows.Remove(row);removed=true;changed++;continue;}
                bool recording=IsRecordingWindow(booking,now);
                if(!states.TryGetValue(booking.Id,out bool previous)||previous!=recording){states[booking.Id]=recording;grid.InvalidateRow(row.Index);changed++;}
            }
            var ids=grid.Rows.Cast<DataGridViewRow>().Select(row=>(row.Tag as Booking)?.Id).ToHashSet();
            foreach(var id in states.Keys.Where(id=>!ids.Contains(id)).ToArray())states.Remove(id);
            if(removed){ApplyDatePartitions(grid);UpdateDisabledCaption();_engineStatus.Text=$"予約 {_nativeReservations.Rows.Count}件 / 録画: TVRock";}
        }
        return changed;
    }
    void FitReservationColumns(DataGridView grid)
    {
        if(_fittingReservationColumns||!grid.Columns.Contains("title")||grid.ClientSize.Width<100)return;
        _fittingReservationColumns=true;
        try {
            var title=grid.Columns["title"];title.MinimumWidth=60;title.AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;
            int available=Math.Max(40,grid.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-64);
            var fixedColumns=grid.Columns.Cast<DataGridViewColumn>().Where(c=>c.Visible&&c!=title).ToArray();
            int sum=fixedColumns.Sum(c=>c.Width);
            if(sum>available){double scale=(double)available/sum;foreach(var c in fixedColumns)c.Width=Math.Max(c.MinimumWidth,(int)(c.Width*scale));}
            grid.HorizontalScrollingOffset=0;
        }finally{_fittingReservationColumns=false;}
    }
}
