using System.Diagnostics;
using System.Runtime.InteropServices;
namespace CMCutLab;
// One gesture owner for the native video child and the managed timeline.
internal sealed class RightShuttleInput:IDisposable
{
 readonly Form owner;readonly Control[] targets;readonly Action<double?> changed;readonly HookProc callback;readonly System.Windows.Forms.Timer releaseCheck=new(){Interval=40};IntPtr hook;bool previousRight;readonly System.Windows.Forms.Timer poll=new(){Interval=10};bool held;int origin;double last=double.NaN;
 internal long GestureCount{get;private set;}
 delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
 [StructLayout(LayoutKind.Sequential)]struct MouseData{public Point Point;public uint MouseDataValue,Flags,Time;public IntPtr Extra;}
 [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
 [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point point);
 [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
 [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string? module);
 internal RightShuttleInput(Form owner,Control[] targets,Action<double?> changed){this.owner=owner;this.targets=targets;this.changed=changed;callback=OnMouse;releaseCheck.Tick+=(_,_)=>{if(held&&(GetAsyncKeyState(2)&0x8000)==0)End();};owner.Deactivate+=Deactivate;}
 internal void Start(){if(poll.Enabled)return;previousRight=(GetAsyncKeyState(2)&0x8000)!=0;poll.Tick+=Poll;poll.Start();}
 void Poll(object? sender,EventArgs e){bool down=(GetAsyncKeyState(2)&0x8000)!=0;Point point=Cursor.Position;if(down&&!previousRight&&!held&&Hit(point))Begin(point.X);if(held){if(!down)End();else Move(point.X);}previousRight=down;}
 bool Hit(Point point)=>owner.Visible&&owner.Enabled&&GetAncestor(WindowFromPoint(point),2)==owner.Handle&&targets.Any(c=>c.Visible&&c.Enabled&&c.RectangleToScreen(c.ClientRectangle).Contains(point));
 IntPtr OnMouse(int code,IntPtr message,IntPtr data){if(code<0)return CallNextHookEx(hook,code,message,data);int kind=message.ToInt32();var mouse=Marshal.PtrToStructure<MouseData>(data);if(kind==0x204&&!held&&Hit(mouse.Point)){Begin(mouse.Point.X);return CallNextHookEx(hook,code,message,data);}if(held&&kind==0x200){Move(mouse.Point.X);return CallNextHookEx(hook,code,message,data);}if(held&&kind==0x205){End();return CallNextHookEx(hook,code,message,data);}return CallNextHookEx(hook,code,message,data);}
 internal void Begin(int x,bool monitorRelease=true){held=true;origin=x;last=double.NaN;GestureCount++;if(monitorRelease)releaseCheck.Start();changed(0);}
 internal void Move(int x){if(!held)return;double distance=(x-origin)*96.0/Math.Max(96,owner.DeviceDpi);if(Math.Abs(distance-last)<.1)return;last=distance;changed(distance);}
 internal void End(){if(!held)return;held=false;releaseCheck.Stop();changed(null);}
 void Deactivate(object? sender,EventArgs e){if(owner.IsHandleCreated&&!owner.IsDisposed)owner.BeginInvoke(()=>{if(!owner.IsDisposed&&GetAncestor(GetForegroundWindow(),2)!=owner.Handle)End();});}
 public void Dispose(){End();owner.Deactivate-=Deactivate;releaseCheck.Dispose();poll.Stop();poll.Tick-=Poll;poll.Dispose();if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}
}
internal sealed class PaneSplit:SplitContainer
{
 internal PaneSplit(){SetStyle(ControlStyles.ResizeRedraw,true);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(Color.FromArgb(88,94,101),1);var r=SplitterRectangle;if(Orientation==Orientation.Vertical)e.Graphics.DrawLine(pen,r.Left+r.Width/2,r.Top,r.Left+r.Width/2,r.Bottom);else e.Graphics.DrawLine(pen,r.Left,r.Top+r.Height/2,r.Right,r.Top+r.Height/2);}
}
