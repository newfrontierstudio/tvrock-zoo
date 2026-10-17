using System.Text.Json;
namespace TsScheduler;
internal sealed partial class MainForm
{
    internal static string? TestWindowPlacementFile;
    readonly System.Windows.Forms.Timer _windowSaveDelay=new(){Interval=600};
    FormWindowState _lastNonMinimizedState=FormWindowState.Normal;
    bool _windowPlacementReady;
    sealed class WindowPlacement {public int X{get;set;}public int Y{get;set;}public int Width{get;set;}public int Height{get;set;}public bool Maximized{get;set;}}
    static string WindowPlacementFile=>TestWindowPlacementFile??Path.Combine(RecommendationHome,"window-placement.json");
    internal static Rectangle FitWindowToArea(Rectangle bounds,Rectangle area,Size minimum)
    {
        int width=Math.Clamp(bounds.Width,Math.Min(minimum.Width,area.Width),area.Width),height=Math.Clamp(bounds.Height,Math.Min(minimum.Height,area.Height),area.Height);
        return new Rectangle(Math.Clamp(bounds.X,area.Left,area.Right-width),Math.Clamp(bounds.Y,area.Top,area.Bottom-height),width,height);
    }
    void InitializeWindowPlacement()
    {
        RestoreWindowPlacement();
        Shown+=(_,_)=>_windowPlacementReady=true;
        Move+=(_,_)=>QueueWindowPlacement();
        Resize+=(_,_)=>{if(WindowState!=FormWindowState.Minimized)_lastNonMinimizedState=WindowState;QueueWindowPlacement();};
        _windowSaveDelay.Tick+=(_,_)=>{_windowSaveDelay.Stop();SaveWindowPlacement();};
        FormClosing+=(_,_)=>SaveWindowPlacement();
        Disposed+=(_,_)=>_windowSaveDelay.Dispose();
    }
    void QueueWindowPlacement(){if(!_windowPlacementReady||WindowState==FormWindowState.Minimized)return;_windowSaveDelay.Stop();_windowSaveDelay.Start();}
    void SaveWindowPlacement()
    {
        if(!_windowPlacementReady)return;
        try {
            var bounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
            if(bounds.Width<=0||bounds.Height<=0)return;
            var data=new WindowPlacement{X=bounds.X,Y=bounds.Y,Width=bounds.Width,Height=bounds.Height,Maximized=(WindowState==FormWindowState.Minimized?_lastNonMinimizedState:WindowState)==FormWindowState.Maximized};
            Directory.CreateDirectory(Path.GetDirectoryName(WindowPlacementFile)!);File.WriteAllText(WindowPlacementFile+".tmp",JsonSerializer.Serialize(data));File.Move(WindowPlacementFile+".tmp",WindowPlacementFile,true);
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException){Log("ウィンドウ位置の保存: "+e.Message);}
    }
    void RestoreWindowPlacement()
    {
        try {
            if(!File.Exists(WindowPlacementFile))return;
            var data=JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(WindowPlacementFile));if(data==null||data.Width<=0||data.Height<=0)return;
            var bounds=new Rectangle(data.X,data.Y,data.Width,data.Height);var area=Screen.FromRectangle(bounds).WorkingArea;
            StartPosition=FormStartPosition.Manual;Bounds=FitWindowToArea(bounds,area,MinimumSize);
            _lastNonMinimizedState=data.Maximized?FormWindowState.Maximized:FormWindowState.Normal;WindowState=_lastNonMinimizedState;
        }catch(Exception e)when(e is IOException or JsonException or ArgumentException){Log("ウィンドウ位置の復元: "+e.Message);}
    }
}
