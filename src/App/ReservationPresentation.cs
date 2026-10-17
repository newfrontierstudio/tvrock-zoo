using TsRecommend;
namespace TsScheduler;
internal sealed partial class MainForm
{
    bool _fittingReservationColumns;
    readonly System.Windows.Forms.Timer _recordingTintTimer=new(){Interval=1000};
    internal static bool IsRecordingWindow(Booking booking,DateTimeOffset now)=>booking.Enabled&&booking.Start<=now&&booking.End>now;
    void InitializeReservationPresentation()
    {
        foreach(var grid in new[]{_nativeReservations,_disabledReservations})
        {
            grid.ScrollBars=ScrollBars.Vertical;
            grid.SizeChanged+=(_,_)=>FitReservationColumns(grid);
            grid.ColumnWidthChanged+=(_,_)=>FitReservationColumns(grid);
            grid.CellFormatting+=(_,e)=>{
                if(e.RowIndex<0||grid.Rows[e.RowIndex].Tag is not Booking b)return;
                if(IsRecordingWindow(b,DateTimeOffset.Now)){e.CellStyle.BackColor=Color.FromArgb(255,238,238);e.CellStyle.SelectionBackColor=Color.FromArgb(250,219,219);}
            };
            FitReservationColumns(grid);
        }
        _recordingTintTimer.Tick+=(_,_)=>_nativeReservations.Invalidate();_recordingTintTimer.Start();
        Disposed+=(_,_)=>_recordingTintTimer.Dispose();
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
