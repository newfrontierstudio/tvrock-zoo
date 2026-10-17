using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
namespace TsScheduler;

internal static class RoundedControls
{
    static readonly ConditionalWeakTable<Control,object> Installed=new();
    internal static GraphicsPath Shape(Rectangle bounds,float radius)
    {
        var path=new GraphicsPath();float diameter=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));
        if(diameter<=0)return path;
        path.AddArc(bounds.Left,bounds.Top,diameter,diameter,180,90);path.AddArc(bounds.Right-diameter,bounds.Top,diameter,diameter,270,90);
        path.AddArc(bounds.Right-diameter,bounds.Bottom-diameter,diameter,diameter,0,90);path.AddArc(bounds.Left,bounds.Bottom-diameter,diameter,diameter,90,90);path.CloseFigure();return path;
    }
    internal static void Apply(Control control)
    {
        if(Installed.TryGetValue(control,out _))return;
        Installed.Add(control,new object());
        void Update(){if(control.IsDisposed||control.ClientSize.Width<1||control.ClientSize.Height<1)return;using var path=Shape(control.ClientRectangle,4*control.DeviceDpi/96f);var previous=control.Region;control.Region=new Region(path);previous?.Dispose();}
        control.SizeChanged+=(_,_)=>Update();control.DpiChangedAfterParent+=(_,_)=>Update();Update();
    }
}
