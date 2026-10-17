namespace TsScheduler;
internal sealed partial class MainForm
{
    internal static void ClearReservationSelection(DataGridView grid){grid.CurrentCell=null;grid.ClearSelection();}
    void InitializeReservationDeselection()
    {
        var filter=new ReservationClickFilter(this);Application.AddMessageFilter(filter);
        Disposed+=(_,_)=>Application.RemoveMessageFilter(filter);
        Deactivate+=(_,_)=>{ClearReservationSelection(_nativeReservations);ClearReservationSelection(_disabledReservations);};
    }
    sealed class ReservationClickFilter(MainForm owner):IMessageFilter
    {
        public bool PreFilterMessage(ref Message message)
        {
            if(message.Msg!=0x0201||owner.IsDisposed)return false;
            var clicked=Control.FromChildHandle(message.HWnd);
            if(clicked==null||clicked.FindForm()!=owner)return false;
            foreach(var grid in new[]{owner._nativeReservations,owner._disabledReservations}){
                bool inside=clicked==grid||grid.Contains(clicked);
                if(!inside){ClearReservationSelection(grid);continue;}
                var hit=grid.HitTest(grid.PointToClient(Cursor.Position).X,grid.PointToClient(Cursor.Position).Y);
                if(hit.Type==DataGridViewHitTestType.None)owner.BeginInvoke(new Action(()=>{if(!grid.IsDisposed)ClearReservationSelection(grid);}));
            }
            return false;
        }
    }
}
