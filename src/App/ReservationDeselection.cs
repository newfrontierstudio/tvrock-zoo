namespace TsScheduler;
internal sealed partial class MainForm
{
    internal static void ClearReservationSelection(DataGridView grid)
    {
        if(grid.IsDisposed)return;
        if(grid.IsCurrentCellInEditMode&&!grid.EndEdit())return;
        grid.CurrentCell=null;grid.ClearSelection();
    }
    void InitializeReservationDeselection()
    {
        var filter=new ReservationClickFilter(this);Application.AddMessageFilter(filter);
        Disposed+=(_,_)=>Application.RemoveMessageFilter(filter);
    }
    static System.Collections.Generic.IEnumerable<Control> SelectionSurfaces(Control root)
    {
        foreach(Control child in root.Controls){
            if(child is DataGridView or ScheduleGrid or RecordedMediaView || child is ListBox and not CheckedListBox){yield return child;continue;}
            foreach(var surface in SelectionSurfaces(child))yield return surface;
        }
    }
    internal static bool IsSelectionBlank(Control clicked,Point location)
    {
        // A media card (including its unused right-hand area) is a selectable item.
        for(Control? parent=clicked;parent!=null;parent=parent.Parent)if(parent.Tag is ValueTuple<RecordedFile,CheckBox>)return false;
        for(Control? current=clicked;current!=null;current=current.Parent){if(current is TabPage)return false;if(current is MainForm owner){
            // Only unused space above the tabs clears selection; settings content stays active.
            return clicked is Panel or MainForm && clicked.PointToScreen(location).Y<owner._tabs.PointToScreen(Point.Empty).Y;
        }}
        return false;
    }
    internal void ClearAllTabSelections()
    {
        foreach(var surface in SelectionSurfaces(this)){
            if(surface.IsDisposed)continue;
            switch(surface){
                case DataGridView grid:ClearReservationSelection(grid);break;
                case ScheduleGrid schedule:if(schedule.Selected!=null){schedule.Selected=null;schedule.Invalidate();}break;
                case RecordedMediaView media:media.ClearLibrarySelection();break;
                case ListBox list when list.AccessibleName!="設定メニュー":list.ClearSelected();break;
            }
        }
        HideLargeTip();
    }
    sealed class ReservationClickFilter(MainForm owner):IMessageFilter
    {
        public bool PreFilterMessage(ref Message message)
        {
            if(message.Msg!=0x0201||owner.IsDisposed||owner.Disposing)return false;
            var clicked=Control.FromChildHandle(message.HWnd);
            // Studio embeds MainForm; do not rely on FindForm identifying the outer window.
            if(clicked==null||(clicked!=owner&&!owner.Contains(clicked)))return false;
            if(!IsSelectionBlank(clicked,clicked.PointToClient(Cursor.Position)))return false;
            // Let the control finish handling its click before clearing CurrentCell as well.
            if(owner.IsHandleCreated)owner.BeginInvoke(new Action(()=>{if(!owner.IsDisposed&&!owner.Disposing)owner.ClearAllTabSelections();}));
            return false;
        }
    }
}
