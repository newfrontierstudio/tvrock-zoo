namespace TsScheduler;
internal sealed partial class MainForm
{
    internal static IEnumerable<TsRecommend.Booking> ReservationRows(IEnumerable<TsRecommend.Booking> bookings,bool merged,bool includePast,DateTimeOffset now)=>bookings.Where(b=>(merged||b.Enabled)&&(includePast||b.End>now)).OrderBy(b=>b.Start);
    bool _mergeReservations;
    readonly ToolStripMenuItem _splitViewItem=new("上下に分けて表示");
    readonly ToolStripMenuItem _mergedViewItem=new("日時順にまとめて表示（○・×）");
    void InitializeReservationViewMenu()
    {
        var menu=new ContextMenuStrip();menu.Items.AddRange([_splitViewItem,_mergedViewItem]);
        _splitViewItem.Checked=true;
        _splitViewItem.Click+=(_,_)=>SetReservationView(false);
        _mergedViewItem.Click+=(_,_)=>SetReservationView(true);
        _tabs.TabPages[0].Text="予約一覧 ▾";
        _tabs.AccessibleDescription="予約一覧タブを右クリックすると分割表示と日時順のまとめ表示を切り替えます";
        _tabs.MouseUp+=(_,e)=>{if(e.Button==MouseButtons.Right&&_tabs.GetTabRect(0).Contains(e.Location)){_tabs.SelectedIndex=0;menu.Show(_tabs,e.Location);}};
        Disposed+=(_,_)=>menu.Dispose();
    }
    void SetReservationView(bool merged,bool reload=true)
    {
        string? selected=(_nativeReservations.CurrentRow?.Tag as TsRecommend.Booking)?.Id??(_disabledReservations.CurrentRow?.Tag as TsRecommend.Booking)?.Id;
        _mergeReservations=merged;_splitViewItem.Checked=!merged;_mergedViewItem.Checked=merged;
        _applyingReservationSplit=true;try{_reservationSplit.Panel2Collapsed=merged;}finally{_applyingReservationSplit=false;}
        if(!merged)ApplyReservationSplit();
        if(reload){LoadNativeReservations();if(selected!=null){var booking=_nativeBookings.FirstOrDefault(b=>b.Id==selected);if(booking!=null)RevealChangedReservation(booking.Id,booking.Enabled);}QueueLayoutSave();}
    }
}
