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
        foreach(DataGridViewColumn c in _nativeReservations.Columns)_disabledReservations.Columns.Add((DataGridViewColumn)c.Clone());
        _disabledReservations.DefaultCellStyle=_nativeReservations.DefaultCellStyle.Clone();_disabledReservations.ColumnHeadersDefaultCellStyle=_nativeReservations.ColumnHeadersDefaultCellStyle.Clone();_disabledReservations.EnableHeadersVisualStyles=false;
        _disabledReservations.RowTemplate.Height=_nativeReservations.RowTemplate.Height;_disabledBox.Controls.Add(_disabledReservations);
        var toolbar=page.Controls.OfType<FlowLayoutPanel>().First();page.Controls.Clear();var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,0));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,180));toolbar.Visible=false;toolbar.Dock=DockStyle.Fill;_disabledBox.Dock=DockStyle.Fill;layout.Controls.Add(toolbar,0,0);layout.Controls.Add(_nativeReservations,0,1);layout.Controls.Add(_disabledBox,0,2);page.Controls.Add(layout);
        _nativeReservations.CellClick+=async(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex==0&&_nativeReservations.Rows[e.RowIndex].Tag is Booking b)await ChangeReservationEnabled(b,false);};
        _disabledReservations.CellClick+=async(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex==0&&_disabledReservations.Rows[e.RowIndex].Tag is Booking b)await ChangeReservationEnabled(b,true);};
        _nativeReservations.Columns[0].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;_disabledReservations.Columns[0].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
    }
    void LoadDisabledReservations()
    {
        if(_disabledReservations.Columns.Count==0)return;_disabledReservations.Rows.Clear();
        foreach(var b in _nativeBookings.Where(b=>!b.Enabled&&(_includeFinishedBookings.Checked||b.End>DateTimeOffset.Now)).OrderBy(b=>b.Start)){
            int i=_disabledReservations.Rows.Add("×",b.Start.LocalDateTime,(int)Math.Round((b.End-b.Start).TotalMinutes),b.Station,"",b.Title);var row=_disabledReservations.Rows[i];row.Tag=b;row.Cells[1].Style.Format="MM/dd(ddd) HH:mm";row.Cells[2].Style.Format="0分";row.Cells[3].Style.ForeColor=StationColor(b.Station);
        }
        UpdateDisabledCaption();
    }
    async Task ChangeReservationEnabled(Booking booking,bool enable)
    {
        if(_changingReservation)return;_changingReservation=true;
        try{
            var current=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch")).SingleOrDefault(b=>b.Id==booking.Id&&b.Start==booking.Start&&Data.Norm(b.Title)==Data.Norm(booking.Title));if(current==null)throw new IOException("予約が更新されています。一覧を更新してください。");if(current.Enabled==enable){LoadNativeReservations();return;}
            if(current.Start<DateTimeOffset.Now.AddMinutes(2)&&current.End>DateTimeOffset.Now)throw new InvalidOperationException("開始直前・録画中の予約です。TVRockの詳細画面から操作してください。");
            string folder=Path.Combine(RecommendationHome,"reservation-changes",DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(folder);File.Copy(Path.Combine(RockFolder,"tvrock.sch"),Path.Combine(folder,"before.sch"));
            string html=await NativeGet("reg?i="+current.Id);File.WriteAllText(Path.Combine(folder,"before.html"),html);var fields=Data.Form(html,"予約を変更");if(fields.GetValueOrDefault("i")!=current.Id||Data.Norm(fields.GetValueOrDefault("title",""))!=Data.Norm(current.Title))throw new InvalidDataException("予約フォームの照合に失敗しました。");
            if(enable)fields["valid"]="true";else fields.Remove("valid");
            await NativeGet("regsch?"+string.Join("&",fields.Select(p=>Data.Encode(p.Key)+"="+Data.Encode(p.Value))));await Task.Delay(400);
            var updated=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch")).SingleOrDefault(b=>b.Id==current.Id);if(updated==null||updated.Enabled!=enable)throw new IOException("反映を確認できません。一覧を更新して状態をご確認ください。");
            RecordReservationChange(current,enable);
            File.WriteAllText(Path.Combine(folder,"result.txt"),current.Id+" enabled="+enable);LoadNativeReservations();
        }catch(Exception e){MessageBox.Show(this,e.Message,"予約の切替");}finally{_changingReservation=false;}
    }
}
