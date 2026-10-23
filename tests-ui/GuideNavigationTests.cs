using System.Reflection;
using TsRecommend;
using TsScheduler;
namespace TsScheduler.Tests;
internal static class GuideNavigationTests
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    internal static void Run()
    {
        using var host=new Form{ClientSize=new Size(520,600)};using var grid=new ScheduleGrid{Font=new Font("Meiryo UI",11)};host.Controls.Add(grid);host.CreateControl();grid.CreateControl();grid.Day=new DateTime(2026,10,23);
        var start=new DateTimeOffset(grid.Day,TimeSpan.FromHours(9)).AddHours(1);var show=new Show("hover",1048,1,"TBS","ものスタ","番組詳細",start,start.AddHours(5),[]);grid.Shows=[show];grid.RefreshSchedule();
        Show? hovered=null;int clicks=0,edits=0;grid.HoverShow=(s,_)=>hovered=s;grid.ContextShow=(_,_)=>clicks++;grid.BookShow=_=>edits++;
        void Mouse(string method,Point p,MouseButtons button=MouseButtons.None,int delta=0)=>typeof(ScheduleGrid).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(grid,[new MouseEventArgs(button,1,p.X,p.Y,delta)]);
        foreach(bool booked in new[]{false,true}){
            grid.Booked=booked?[show.Key]:[];using var image=new Bitmap(grid.Width,grid.Height);grid.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
            var hits=(List<(Rectangle Bounds,Show Show)>)typeof(ScheduleGrid).GetField("_hoverHits",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(grid)!;
            var area=hits.First(h=>h.Show.Key==show.Key).Bounds;Mouse("OnMouseMove",new Point(area.Left+2,area.Top+2));if(hovered?.Key!=show.Key)throw new Exception("Programme text did not show details");
            var blank=new Point(90,450);Mouse("OnMouseMove",blank);if(hovered!=null)throw new Exception("Blank area below text still showed programme details");
            Mouse("OnMouseClick",blank,MouseButtons.Right);Mouse("OnMouseDoubleClick",blank,MouseButtons.Left);
        }
        if(clicks!=2||edits!=2)throw new Exception("Blank-area click/context/double-click behaviour changed");
        // Dispatch Windows messages, rather than calling the event handler directly.
        grid.Booked=[];edits=0;
        var titleBounds=((List<(Rectangle Bounds,Show Show)>)typeof(ScheduleGrid).GetField("_hoverHits",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(grid)!).Last().Bounds;
        var point=new Point(titleBounds.Left+2,titleBounds.Top+2);var position=(IntPtr)((point.Y<<16)|point.X);
        host.Show();Application.DoEvents();
        using(var card=new FlatHoverCard()){
            var hit=SendMessage(card.Handle,0x84,IntPtr.Zero,position);
            if(hit!=(IntPtr)(-1))throw new Exception("Details popup must pass title clicks to the programme");
        }
        var savedCursor=Cursor.Position;Cursor.Position=grid.PointToScreen(point);
        try{
        SendMessage(grid.Handle,0x201,(IntPtr)1,position);
        SendMessage(grid.Handle,0x202,IntPtr.Zero,position);
        SendMessage(grid.Handle,0x203,(IntPtr)1,position);
        SendMessage(grid.Handle,0x202,IntPtr.Zero,position);
        }finally{Cursor.Position=savedCursor;host.Hide();}
        if(edits!=1)throw new Exception($"Windows double-click must request exactly one booking; actual={edits}");
        int turns=0;grid.DayBoundary=direction=>{turns+=direction;grid.Day=grid.Day.AddDays(direction);grid.AutoScrollPosition=direction>0?Point.Empty:new Point(0,int.MaxValue);};
        grid.AutoScrollPosition=new Point(0,int.MaxValue);Mouse("OnMouseWheel",Point.Empty,delta:-120);if(turns!=1||grid.AutoScrollPosition.Y!=0)throw new Exception("Bottom wheel did not advance to next day's top");
        Mouse("OnMouseWheel",Point.Empty,delta:120);if(turns!=1)throw new Exception("Wheel burst changed multiple dates");
        typeof(ScheduleGrid).GetField("_lastDayTurn",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(grid,0L);Mouse("OnMouseWheel",Point.Empty,delta:120);if(turns!=0||grid.AutoScrollPosition.Y>=0)throw new Exception("Top wheel did not return to previous day's bottom");
    }
}
