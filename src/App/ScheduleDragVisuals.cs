namespace TsScheduler;
internal sealed partial class ScheduleGrid
{
 Point _dragPointer;bool _dragVisual;
 void InitializeDragVisuals(){}
 void UpdateDragVisual(Point point){_dragPointer=point;if(_headerDown is Point start&&Math.Abs(point.X-start.X)+Math.Abs(point.Y-start.Y)>5)_dragVisual=true;Invalidate();}
 void EndDragVisual(){_dragVisual=false;Invalidate();}
 void DrawStationDragBadge(Graphics graphics,string station,Rectangle bounds)=>StationBadgeRenderer.Draw(graphics,station,Font,bounds,centered:true);
 void DrawDragOverlay(Graphics graphics){if(!_dragVisual||_dragStation==null)return;var stations=OrderedStations();int index=Array.IndexOf(stations,HeaderStation(_dragPointer)),from=Array.IndexOf(stations,_dragStation);if(index<0||from==index)return;int x=65+index*ColumnWidth+AutoScrollPosition.X+(from<index?ColumnWidth:0);x=Math.Clamp(x,65,Math.Max(65,ClientSize.Width-_vertical.Width-1));using var pen=new Pen(Color.Black,1);graphics.DrawLine(pen,x,0,x,ClientSize.Height);}
 protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&(_headerDown!=null||_dragVisual)){_headerDown=null;_dragStation=null;if(!IsDisposed&&!Disposing)EndDragVisual();}}
}