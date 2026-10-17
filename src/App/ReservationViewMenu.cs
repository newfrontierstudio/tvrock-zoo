namespace TsScheduler;
internal sealed partial class MainForm
{
    internal static IEnumerable<TsRecommend.Booking> ReservationRows(IEnumerable<TsRecommend.Booking> bookings,bool merged,bool includePast,DateTimeOffset now)=>bookings.Where(b=>(merged||b.Enabled)&&(includePast||b.End>now)).OrderBy(b=>b.Start);
    internal static Rectangle ReservationArrowBounds(Rectangle tab,int dpi)=>new(tab.Right-(int)(32*dpi/96f),tab.Top,(int)(32*dpi/96f),tab.Height);
    bool _mergeReservations;
    readonly ToolStripMenuItem _splitViewItem=new("上下に分けて表示");
    readonly ToolStripMenuItem _mergedViewItem=new("日時順にまとめて表示（○・×）");
    void InitializeReservationViewMenu()
    {
        var menu=new ContextMenuStrip();menu.Items.AddRange([_splitViewItem,_mergedViewItem]);
        _splitViewItem.Checked=true;
        _splitViewItem.Click+=(_,_)=>SetReservationView(false);
        _mergedViewItem.Click+=(_,_)=>SetReservationView(true);
        _tabs.TabPages[0].Text="予約一覧";
        _tabs.AccessibleDescription="予約一覧の右端の▼をクリックすると表示方法を選べます";
        void OpenMenu(){_tabs.SelectedIndex=0;var tab=_tabs.GetTabRect(0);menu.Show(_tabs,new Point(tab.Left,tab.Bottom));}
        _tabs.MouseUp+=(_,e)=>{var tab=_tabs.GetTabRect(0);if((e.Button==MouseButtons.Left&&ReservationArrowBounds(tab,DeviceDpi).Contains(e.Location))||(e.Button==MouseButtons.Right&&tab.Contains(e.Location)))OpenMenu();};
        _tabs.MouseMove+=(_,e)=>_tabs.Cursor=ReservationArrowBounds(_tabs.GetTabRect(0),DeviceDpi).Contains(e.Location)?Cursors.Hand:Cursors.Default;
        _tabs.MouseLeave+=(_,_)=>_tabs.Cursor=Cursors.Default;
        _tabs.KeyDown+=(_,e)=>{if(_tabs.SelectedIndex==0&&e.Alt&&e.KeyCode==Keys.Down){e.SuppressKeyPress=true;OpenMenu();}};
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
