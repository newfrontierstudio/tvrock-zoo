namespace TsScheduler;
internal sealed partial class MainForm
{
    Action? _externalBack,_externalForward;
    internal bool HandleExternalBrowserCommand(ref Message message){if(message.Msg!=0x319)return false;int command=(int)((message.LParam.ToInt64()>>16)&0xFFF);if(command is not(1 or 2 or 3))return false;if(command==1)_externalBack?.Invoke();else if(command==2)_externalForward?.Invoke();else RefreshStudio();message.Result=new IntPtr(1);return true;}
    protected override void WndProc(ref Message message){if(HandleExternalBrowserCommand(ref message))return;base.WndProc(ref message);}
    void InitializeBackGesture()
    {
        _gestureOptions=GestureOptions.Load(RecommendationHome);
        var history=new Stack<TabPage>();var forward=new Stack<TabPage>();TabPage? previous=_tabs.SelectedTab;bool navigating=false;
        _tabs.SelectedIndexChanged+=(_,_)=>{if(!navigating&&previous!=null&&previous!=_tabs.SelectedTab){history.Push(previous);forward.Clear();}previous=_tabs.SelectedTab;};
        void Navigate(Stack<TabPage> source,Stack<TabPage> destination){while(source.Count>0){var page=source.Pop();if(page.IsDisposed||!_tabs.TabPages.Contains(page)||page==_tabs.SelectedTab)continue;if(_tabs.SelectedTab!=null)destination.Push(_tabs.SelectedTab);navigating=true;try{_tabs.SelectedTab=page;}finally{navigating=false;previous=page;}break;}}
        _externalBack=()=>Navigate(history,forward);_externalForward=()=>Navigate(forward,history);
        var filter=new BackGestureFilter(this,()=>Navigate(history,forward),()=>Navigate(forward,history),RefreshStudio,direction=>_gestureOptions.ActionFor(direction));Application.AddMessageFilter(filter);Disposed+=(_,_)=>Application.RemoveMessageFilter(filter);
    }
    internal sealed class BackGestureFilter(Control owner,Action back,Action forward,Action? refresh=null,Func<string,string>? gestureAction=null):IMessageFilter
    {
        Point origin;int top,bottom;bool tracking;long suppressUntil;
        internal static bool IsBackDrag(Point start,Point end,int dpi)=>start.X-end.X>=Math.Max(40,60*dpi/96)&&Math.Abs(end.Y-start.Y)<Math.Max(30,(start.X-end.X)/2);
        internal static bool IsRefreshDrag(Point start,Point end,int top,int bottom,int dpi)=>Math.Max(Math.Abs(end.Y-start.Y),bottom-top)>=Math.Max(40,60*dpi/96)&&Math.Abs(end.X-start.X)<Math.Max(30,(bottom-top)/2);
        internal static string GestureDirection(Point start,Point end,int top,int bottom,int dpi){if(IsBackDrag(start,end,dpi))return "Left";if(IsBackDrag(end,start,dpi))return "Right";if(!IsRefreshDrag(start,end,top,bottom,dpi))return "";return "Vertical";}
        bool Inside(IntPtr handle){for(var c=Control.FromChildHandle(handle);c!=null;c=c.Parent)if(c==owner)return true;return false;}
        public bool PreFilterMessage(ref Message message)
        {
            if(owner.IsDisposed)return false;
            if(message.Msg==0x205&&tracking){tracking=false;if(Inside(message.HWnd)){var end=Cursor.Position;string direction=GestureDirection(origin,end,Math.Min(top,end.Y),Math.Max(bottom,end.Y),owner.DeviceDpi);string action=gestureAction?.Invoke(direction)??"None";if(action!="None"&&direction.Length>0){suppressUntil=Environment.TickCount64+500;if(action=="Back")back();else if(action=="Forward")forward();else if(action=="Refresh")refresh?.Invoke();return true;}}}
            if(!Inside(message.HWnd))return false;
            if(message.Msg==0x100&&((Keys)message.WParam.ToInt32() is Keys.F5 or Keys.BrowserRefresh)&&refresh!=null){refresh();return true;}
            if(message.Msg==0x319){int command=(int)((message.LParam.ToInt64()>>16)&0xFFF);if(command is 1 or 2 or 3){if(command==1)back();else if(command==2)forward();else refresh?.Invoke();return true;}}
            if(message.Msg==0x100&&(Keys)message.WParam.ToInt32() is Keys.BrowserBack or Keys.BrowserForward){if((Keys)message.WParam.ToInt32()==Keys.BrowserBack)back();else forward();return true;}
            if(message.Msg==0x200&&tracking){top=Math.Min(top,Cursor.Position.Y);bottom=Math.Max(bottom,Cursor.Position.Y);}
            if(message.Msg==0x204){origin=Cursor.Position;top=bottom=origin.Y;tracking=true;}
            if(message.Msg==0x1F)tracking=false;
            if(message.Msg==0x7B&&Environment.TickCount64<suppressUntil)return true;
            // Kazaguru Mouse and other tools can send the standard Alt+Left shortcut.
            if((message.Msg==0x104||message.Msg==0x100)&&((Keys)message.WParam.ToInt32() is Keys.Left or Keys.Right)&&((Control.ModifierKeys&Keys.Alt)!=0||(message.LParam.ToInt64()&(1L<<29))!=0)){if((Keys)message.WParam.ToInt32()==Keys.Left)back();else forward();return true;}
            return false;
        }
    }
}
