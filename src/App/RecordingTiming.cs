using System.Text.Json;
using TsRecommend;
namespace TsScheduler;

internal sealed record TimingRule(string Station,string Series,int Head,int Tail);
internal sealed record TimingOnce(string Id,int Head,int Tail);
internal sealed class TimingSettings
{
    public List<TimingRule> Rules{get;set;}=[];
    public List<TimingOnce> Once{get;set;}=[];
    internal static bool Matches(TimingRule rule,Booking booking)=>Data.SameStation(rule.Station,booking.Station)&&Data.Norm(rule.Series).Length>0&&Data.Norm(booking.Title).StartsWith(Data.Norm(rule.Series),StringComparison.Ordinal);
    internal (int Head,int Tail)? Desired(Booking booking)
    {
        var once=Once.LastOrDefault(value=>value.Id==booking.Id);if(once!=null)return(once.Head,once.Tail);
        var rule=Rules.LastOrDefault(value=>Matches(value,booking));return rule==null?null:(rule.Head,rule.Tail);
    }
    internal static TimingSettings Load(string path){if(!File.Exists(path))return new();var settings=JsonSerializer.Deserialize<TimingSettings>(File.ReadAllText(path))??new();settings.Rules=(settings.Rules??[]).Where(value=>value!=null&&!string.IsNullOrWhiteSpace(value.Station)&&!string.IsNullOrWhiteSpace(value.Series)&&Math.Abs((long)value.Head)<=3600&&Math.Abs((long)value.Tail)<=3600).ToList();settings.Once=(settings.Once??[]).Where(value=>value!=null&&!string.IsNullOrWhiteSpace(value.Id)&&Math.Abs((long)value.Head)<=3600&&Math.Abs((long)value.Tail)<=3600).ToList();return settings;}
    internal void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}

internal static class RecordingTiming
{
    internal static (int Head,int Tail) Current(Booking booking)
    {
        int.TryParse(booking.Fields.GetValueOrDefault("READY"),out int ready);int.TryParse(booking.Fields.GetValueOrDefault("TALE"),out int tail);return(-ready,tail);
    }
    internal static Dictionary<string,string> BuildFields(Dictionary<string,string> form,int head,int tail)
    {
        if(Math.Abs((long)head)>3600||Math.Abs((long)tail)>3600)throw new ArgumentOutOfRangeException("調整は±3600秒以内で指定してください。");
        var fields=new Dictionary<string,string>(form);fields["ready"]=(-head).ToString(System.Globalization.CultureInfo.InvariantCulture);fields["tale"]=tail.ToString(System.Globalization.CultureInfo.InvariantCulture);return fields;
    }
    internal static async Task Apply(Booking expected,int head,int tail,Func<List<Booking>> read,Func<string,Task<string>> get,Action backup)
    {
        var current=read().SingleOrDefault(value=>value.Id==expected.Id&&value.Start==expected.Start&&value.End==expected.End&&Data.Norm(value.Title)==Data.Norm(expected.Title));
        if(current==null)throw new IOException("予約内容が変わりました。一覧を更新してください。");
        if(current.End.AddSeconds(tail)<=current.Start.AddSeconds(head))throw new InvalidOperationException("調整後の録画時間が0秒以下です。");
        if(Current(current)==(head,tail))return;
        var form=Data.Form(await get("reg?i="+Uri.EscapeDataString(current.Id)),"予約を変更");
        if(form.GetValueOrDefault("i")!=current.Id||Data.Norm(form.GetValueOrDefault("title",""))!=Data.Norm(current.Title))throw new InvalidDataException("予約フォームを照合できませんでした。");
        var fields=BuildFields(form,head,tail);backup();Exception? sendError=null;
        try{await get("regsch?"+string.Join("&",fields.Select(value=>Data.Encode(value.Key)+"="+Data.Encode(value.Value))));}
        catch(Exception error)when(error is HttpRequestException or TaskCanceledException){sendError=error;}
        for(int attempt=0;attempt<16;attempt++){
            var updated=read().SingleOrDefault(value=>value.Id==current.Id);
            if(updated!=null&&Current(updated)==(head,tail))return;
            await Task.Delay(250);
        }
        throw new IOException("録画時間の反映を確認できません。自動再送は停止しました。一覧を更新して確認してください。",sendError);
    }
}

internal sealed partial class MainForm
{
    TimingSettings _timingSettings=new();bool _timingBusy,_timingFailed;DateTimeOffset _nextTimingCheck=DateTimeOffset.MinValue;
    string TimingFile=>Path.Combine(RecommendationHome,"recording-timing.json");
    void InitializeRecordingTiming()
    {
        try{_timingSettings=TimingSettings.Load(TimingFile);}catch(Exception error)when(error is IOException or JsonException){Log("録画時間設定: "+error.Message);}
        foreach(var grid in new[]{_nativeReservations,_disabledReservations}){
            var menu=new ContextMenuStrip();var edit=new ToolStripMenuItem("録画の開始・終了を調整…");menu.Items.Add(edit);Booking? target=null;
            grid.CellMouseDown+=(_,e)=>{if(e.Button!=MouseButtons.Right)return;target=null;if(e.RowIndex<0)return;target=grid.Rows[e.RowIndex].Tag as Booking;grid.ClearSelection();grid.Rows[e.RowIndex].Selected=true;grid.CurrentCell=grid.Rows[e.RowIndex].Cells["title"];};
            menu.Opening+=(_,e)=>{if(target==null)e.Cancel=true;};edit.Click+=async(_,_)=>{if(target!=null)await ShowRecordingTiming(target);};grid.ContextMenuStrip=menu;
            Disposed+=(_,_)=>menu.Dispose();
        }
    }
    internal static string TimingSeries(string title)
    {
        int cut=title.IndexOfAny(['【','「','▽','▼','～','〜','—','：']);int space=title.IndexOf(' ');if(space>0&&System.Text.RegularExpressions.Regex.IsMatch(title[..space],@"[\u3040-\u30ff\u4e00-\u9fff]")&&(cut<0||space<cut))cut=space;return(cut>0?title[..cut]:title).Trim();
    }
    async Task ShowRecordingTiming(Booking booking)
    {
        if(_timingBusy||_changingReservation)return;
        booking=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch")).SingleOrDefault(value=>value.Id==booking.Id)??booking;
        var current=RecordingTiming.Current(booking);var existing=_timingSettings.Rules.LastOrDefault(rule=>TimingSettings.Matches(rule,booking));
        using var dialog=new Form{Text="録画の開始・終了を調整",ClientSize=new Size(680,520),AutoScaleMode=AutoScaleMode.Dpi,MinimumSize=new Size(620,440),Font=Font,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(18),ColumnCount=2,RowCount=8};panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,160));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));dialog.Controls.Add(shell);shell.Controls.Add(panel,0,0);for(int row=0;row<8;row++)panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var assignment=AssignmentFor(booking);var title=new Label{Text=booking.Station+"  "+booking.Title+"\n使用チューナー："+assignment.Driver,AutoSize=true,MaximumSize=new Size(570,0),Margin=new Padding(0,0,0,12)};panel.Controls.Add(title,0,0);panel.SetColumnSpan(title,2);
        var head=new NumericUpDown{Minimum=-3600,Maximum=3600,Value=Math.Clamp(current.Head,-3600,3600),Width=120};var tail=new NumericUpDown{Minimum=-3600,Maximum=3600,Value=Math.Clamp(current.Tail,-3600,3600),Width=120};
        panel.Controls.Add(new Label{Text="開始：−で早く／＋で遅く",AutoSize=true},0,1);panel.Controls.Add(head,1,1);panel.Controls.Add(new Label{Text="終了：−で短縮／＋で延長",AutoSize=true},0,2);panel.Controls.Add(tail,1,2);
        var note=new Label{Text="秒単位。番組の時刻を基準に上書きします（加算しません）。常時適用はZoo起動中に動作します。",AutoSize=true,MaximumSize=new Size(570,0)};panel.Controls.Add(note,0,3);panel.SetColumnSpan(note,2);
        var always=new CheckBox{Text="この番組は今後も常に（既存予約にも適用）",Checked=existing!=null,AutoSize=true};panel.Controls.Add(always,0,4);panel.SetColumnSpan(always,2);
        var series=new TextBox{Text=existing?.Series??TimingSeries(booking.Title),Dock=DockStyle.Fill,Enabled=always.Checked};panel.Controls.Add(new Label{Text="同じ局・番組名の先頭一致",AutoSize=true},0,5);panel.Controls.Add(series,1,5);
        var preview=new Label{AutoSize=true,MaximumSize=new Size(570,0)};panel.Controls.Add(preview,0,6);panel.SetColumnSpan(preview,2);
        var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Bottom,Padding=new Padding(18,8,18,12)};var apply=new Button{Text="適用",AutoSize=true};var cancel=new Button{Text="キャンセル",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.AddRange([apply,cancel]);
        if(existing!=null){var remove=new Button{Text="この常時設定を解除",AutoSize=true};buttons.Controls.Add(remove);remove.Click+=(_,_)=>{dialog.DialogResult=DialogResult.Yes;dialog.Close();};}shell.Controls.Add(buttons,0,1);dialog.CancelButton=cancel;
        void Preview(){series.Enabled=always.Checked;apply.Enabled=!always.Checked||Data.Norm(series.Text).Length>0;var rule=new TimingRule(booking.Station,series.Text,(int)head.Value,(int)tail.Value);int count=always.Checked?_nativeBookings.Count(value=>value.End>DateTimeOffset.Now&&TimingSettings.Matches(rule,value)):1;preview.Text=$"適用対象 {count}件（録画しない予約も含む）／ 開始 {(int)head.Value:+0;-0;0}秒・終了 {(int)tail.Value:+0;-0;0}秒／録画予定 {booking.Start.AddSeconds((int)head.Value):MM/dd HH:mm:ss} ～ {booking.End.AddSeconds((int)tail.Value):HH:mm:ss}";}
        always.CheckedChanged+=(_,_)=>Preview();series.TextChanged+=(_,_)=>Preview();head.ValueChanged+=(_,_)=>Preview();tail.ValueChanged+=(_,_)=>Preview();Preview();StyleControls(dialog);apply.Click+=(_,_)=>{if(always.Checked&&Data.Norm(series.Text).Length==0)return;dialog.DialogResult=DialogResult.OK;dialog.Close();};
        var result=dialog.ShowDialog(this);
        if(result==DialogResult.Yes&&existing!=null){try{_timingSettings.Rules.Remove(existing);_timingSettings.Save(TimingFile);_timingFailed=false;_engineStatus.Text="この番組の常時設定を解除しました";}catch(IOException error){MessageBox.Show(this,error.Message);}return;}
        if(result!=DialogResult.OK)return;
        int h=(int)head.Value,t=(int)tail.Value;var rule=new TimingRule(booking.Station,series.Text.Trim(),h,t);
        var targets=always.Checked?_nativeBookings.Where(value=>value.End>DateTimeOffset.Now&&TimingSettings.Matches(rule,value)).ToArray():[booking];
        if(targets.Any(value=>value.End.AddSeconds(t)<=value.Start.AddSeconds(h))){MessageBox.Show(this,"調整後の録画時間が0秒以下になる予約があります。");return;}
        if(always.Checked){_timingSettings.Rules.RemoveAll(value=>Data.SameStation(value.Station,rule.Station)&&Data.Norm(value.Series)==Data.Norm(rule.Series));_timingSettings.Rules.Add(rule);var ids=targets.Select(value=>value.Id).ToHashSet();_timingSettings.Once.RemoveAll(value=>ids.Contains(value.Id));}
        else{_timingSettings.Once.RemoveAll(value=>value.Id==booking.Id);_timingSettings.Once.Add(new(booking.Id,h,t));}
        try{_timingSettings.Save(TimingFile);_timingFailed=false;await ApplyRecordingTiming(targets,h,t);}
        catch(Exception error){_timingFailed=true;MessageBox.Show(this,error.Message,"録画時間の調整");}
    }
    async Task ApplyRecordingTiming(IEnumerable<Booking> targets,int head,int tail)
    {
        _timingBusy=true;_changingReservation=true;
        try{int count=0;foreach(var booking in targets){_engineStatus.Text=$"録画時間を調整中 {++count}件";await RecordingTiming.Apply(booking,head,tail,()=>Data.Bookings(Path.Combine(RockFolder,"tvrock.sch")),NativeGet,()=>{string folder=Path.Combine(RecommendationHome,"timing-backups",DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(folder);File.Copy(Path.Combine(RockFolder,"tvrock.sch"),Path.Combine(folder,"before.sch"));});}LoadNativeReservations();_engineStatus.Text=$"録画時間を調整しました {count}件";}
        finally{_timingBusy=false;_changingReservation=false;}
    }
    async Task EnforceRecordingTiming()
    {
        if(_timingBusy||_changingReservation||_timingFailed||DateTimeOffset.Now<_nextTimingCheck||(_timingSettings.Rules.Count==0&&_timingSettings.Once.Count==0))return;
        _nextTimingCheck=DateTimeOffset.Now.AddSeconds(5);
        try{var bookings=Data.Bookings(Path.Combine(RockFolder,"tvrock.sch"));foreach(var booking in bookings.Where(value=>value.End>DateTimeOffset.Now)){var desired=_timingSettings.Desired(booking);if(desired.HasValue&&RecordingTiming.Current(booking)!=desired.Value)await ApplyRecordingTiming([booking],desired.Value.Head,desired.Value.Tail);}}
        catch(Exception error){_timingFailed=true;Log("録画時間の自動適用を停止: "+error);_engineStatus.Text="録画時間の自動適用を停止しました。右クリックから設定を確認してください。";}
    }
}
