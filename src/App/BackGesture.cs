namespace TsScheduler;
internal sealed partial class MainForm
{
    void InitializeBackGesture()
    {
        var history=new Stack<TabPage>();var forward=new Stack<TabPage>();TabPage? previous=_tabs.SelectedTab;bool navigating=false;
        _tabs.SelectedIndexChanged+=(_,_)=>{if(!navigating&&previous!=null&&previous!=_tabs.SelectedTab){history.Push(previous);forward.Clear();}previous=_tabs.SelectedTab;};
        void Navigate(Stack<TabPage> source,Stack<TabPage> destination){while(source.Count>0){var page=source.Pop();if(page.IsDisposed||!_tabs.TabPages.Contains(page)||page==_tabs.SelectedTab)continue;if(_tabs.SelectedTab!=null)destination.Push(_tabs.SelectedTab);navigating=true;try{_tabs.SelectedTab=page;}finally{navigating=false;previous=page;}break;}}
        var filter=new BackGestureFilter(this,()=>Navigate(history,forward),()=>Navigate(forward,history));Application.AddMessageFilter(filter);Disposed+=(_,_)=>Application.RemoveMessageFilter(filter);
    }
    internal sealed class BackGestureFilter(Control owner,Action back,Action forward):IMessageFilter
    {
        Point origin;bool tracking;long suppressUntil;
        internal static bool IsBackDrag(Point start,Point end,int dpi)=>start.X-end.X>=Math.Max(40,60*dpi/96)&&Math.Abs(end.Y-start.Y)<Math.Max(30,(start.X-end.X)/2);
        bool Inside(IntPtr handle){for(var c=Control.FromChildHandle(handle);c!=null;c=c.Parent)if(c==owner)return true;return false;}
        public bool PreFilterMessage(ref Message message)
        {
            if(owner.IsDisposed)return false;
            if(message.Msg==0x205&&tracking){tracking=false;if(Inside(message.HWnd)){var end=Cursor.Position;if(IsBackDrag(origin,end,owner.DeviceDpi)||IsBackDrag(end,origin,owner.DeviceDpi)){suppressUntil=Environment.TickCount64+500;if(end.X<origin.X)back();else forward();return true;}}}
            if(!Inside(message.HWnd))return false;
            if(message.Msg==0x204){origin=Cursor.Position;tracking=true;}
            if(message.Msg==0x1F)tracking=false;
            if(message.Msg==0x7B&&Environment.TickCount64<suppressUntil)return true;
            // Kazaguru Mouse and other tools can send the standard Alt+Left shortcut.
            if(message.Msg==0x104&&((Keys)message.WParam.ToInt32() is Keys.Left or Keys.Right)&&(Control.ModifierKeys&Keys.Alt)!=0){if((Keys)message.WParam.ToInt32()==Keys.Left)back();else forward();return true;}
            return false;
        }
    }
}
