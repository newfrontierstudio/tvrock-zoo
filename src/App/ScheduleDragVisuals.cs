using System.Drawing.Imaging;
namespace TsScheduler;
internal sealed partial class ScheduleGrid
{
    readonly System.Windows.Forms.Timer _dragTimer=new(){Interval=16};
    readonly Dictionary<string,float> _badgeShifts=[];
    Point _dragPointer;string? _dragOver;bool _dragVisual;
    void InitializeDragVisuals(){_dragTimer.Tick+=(_,_)=>{bool moving=false;foreach(var station in OrderedStations()){float value=_badgeShifts.GetValueOrDefault(station),target=_dragVisual&&station==_dragOver&&station!=_dragStation?(_dragPointer.X>(_headerDown?.X??0)?40*DeviceDpi/96f:-40*DeviceDpi/96f):0;float next=value+(target-value)*.3f;if(Math.Abs(next-target)<.5f)next=target;else moving=true;_badgeShifts[station]=next;}Invalidate();if(!moving)_dragTimer.Stop();};}
    void UpdateDragVisual(Point point){_dragPointer=point;if(_headerDown is Point start&&Math.Abs(point.X-start.X)+Math.Abs(point.Y-start.Y)>5)_dragVisual=true;_dragOver=HeaderStation(point);_dragTimer.Start();Invalidate();}
    void EndDragVisual(){_dragVisual=false;_dragOver=null;_dragTimer.Start();Invalidate();}
    void DrawAlphaBadge(Graphics graphics,string station,Rectangle rectangle,float opacity){if(rectangle.Width<1||rectangle.Height<1)return;using var image=new Bitmap(rectangle.Width,rectangle.Height);using(var buffer=Graphics.FromImage(image)){buffer.Clear(Color.Transparent);StationBadgeRenderer.Draw(buffer,station,Font,new Rectangle(Point.Empty,image.Size),centered:true);}using var attributes=new ImageAttributes();attributes.SetColorMatrix(new ColorMatrix{Matrix33=opacity});graphics.DrawImage(image,rectangle,0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);}
    void DrawStationDragBadge(Graphics graphics,string station,Rectangle bounds){bounds.Offset((int)_badgeShifts.GetValueOrDefault(station),0);if(_dragVisual&&station==_dragStation)DrawAlphaBadge(graphics,station,bounds,.25f);else StationBadgeRenderer.Draw(graphics,station,Font,bounds,centered:true);}
    void DrawDragOverlay(Graphics graphics){if(!_dragVisual||_dragStation==null)return;var stations=OrderedStations();int index=Array.IndexOf(stations,_dragOver),from=Array.IndexOf(stations,_dragStation);if(index>=0&&from!=index){int x=65+index*ColumnWidth+AutoScrollPosition.X+(from<index?ColumnWidth:0);x=Math.Clamp(x,65,Math.Max(65,ClientSize.Width-_vertical.Width-3));using var pen=new Pen(Color.FromArgb(88,94,101),2);graphics.DrawLine(pen,x,4,x,36);}DrawAlphaBadge(graphics,_dragStation,new Rectangle(_dragPointer.X-46,_dragPointer.Y-17,92,34),.68f);}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&(_headerDown!=null||_dragVisual)){_headerDown=null;_dragStation=null;if(!IsDisposed&&!Disposing)EndDragVisual();}}
    protected override void Dispose(bool disposing){if(disposing)_dragTimer.Dispose();base.Dispose(disposing);}
}
