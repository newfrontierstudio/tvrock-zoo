using TsRecommend;
namespace TsScheduler;
internal sealed partial class ScheduleGrid : UserControl {
 readonly ModernScrollBar _vertical=new(){Dock=DockStyle.Right};Size _contentSize;
 public new Point AutoScrollPosition {get=>new(0,-_vertical.Value);set{_vertical.Value=Math.Clamp(Math.Abs(value.Y),0,Math.Max(0,_vertical.Maximum-_vertical.LargeChange+1));Invalidate();}}
 public new Size AutoScrollMinSize {get=>_contentSize;set{_contentSize=value;UpdateBars();}}
 void UpdateBars(){if(_vertical==null)return;_contentSize=new Size(ClientSize.Width,_contentSize.Height);_vertical.LargeChange=Math.Max(1,ClientSize.Height-40);_vertical.SmallChange=45;_vertical.Maximum=Math.Max(0,_contentSize.Height-1);}
 protected override void OnResize(EventArgs e){base.OnResize(e);UpdateBars();Invalidate();}
 protected override void OnMouseWheel(MouseEventArgs e){int next=Math.Clamp(_vertical.Value-e.Delta,0,Math.Max(0,_vertical.Maximum-_vertical.LargeChange+1));_vertical.Value=next;}
 public Action? ViewChanged;
 public List<Show> Shows=[];public Func<string,Color> StationInk=_=>Color.FromArgb(24,54,90);public HashSet<string> Booked=[];public Dictionary<string,GuideAssignment> Assignments=[];public HashSet<string>? RecordingKeys;public HashSet<string> Disabled=[];public List<string> StationOrder=[];public DateTime Day=DateTime.Today;public int VisibleDays=>1;public int DurationMinutes=>1440;public Show? Selected;public Action<Show?,Point>? HoverShow;public Action<Show>? SelectShow;readonly List<(Rectangle Bounds,Show Show)> _hits=[];string? _lastHover;
 int ColumnWidth=>Math.Max(1,(ClientSize.Width-65-_vertical.Width)/Math.Max(1,Shows.Select(s=>s.Station).Distinct().Count()));
 internal static int TimelineY(double minute)=>40+(int)(minute*3)+Math.Min(2,(int)(minute/480))*30;
 public Action<Show>? BookShow;
 public Action<List<string>>? StationOrderChanged;
 Point? _headerDown;string? _dragStation;bool _skipClick;DateTime _lastThinBooking;string? _lastThinKey;
 internal static bool CompactFrame(Rectangle bounds,Font font)=>bounds.Height<font.Height*2;
 string[] OrderedStations()=>Shows.Select(s=>s.Station).Distinct().OrderBy(s=>StationOrder.IndexOf(s)<0?int.MaxValue:StationOrder.IndexOf(s)).ThenBy(s=>s,StringComparer.CurrentCulture).ToArray();
 string? HeaderStation(Point point){var stations=OrderedStations();int index=(point.X-65-AutoScrollPosition.X)/ColumnWidth;return point.X>=65&&index>=0&&index<stations.Length?stations[index]:null;}
 internal static List<string> MoveStation(IEnumerable<string> stations,string source,string target){var result=stations.Distinct().ToList();int from=result.IndexOf(source),to=result.IndexOf(target);if(from>=0&&to>=0&&from!=to){result.RemoveAt(from);result.Insert(to,source);}return result;}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&IsHeader(e.Location)&&HeaderStation(e.Location) is string station){_headerDown=e.Location;_dragStation=station;Capture=true;}}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(_headerDown is Point start&&_dragStation is string source){if(Math.Abs(e.X-start.X)+Math.Abs(e.Y-start.Y)>5&&HeaderStation(e.Location) is string target&&target!=source){StationOrder=MoveStation(OrderedStations(),source,target);StationOrderChanged?.Invoke(StationOrder);_skipClick=true;Invalidate();}_headerDown=null;_dragStation=null;Capture=false;EndDragVisual();Cursor=Cursors.Default;}}
 protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(e.Button==MouseButtons.Left&&e.X>=65&&!IsHeader(e.Location)){var hit=_hits.LastOrDefault(h=>h.Bounds.Contains(e.Location));if(hit.Show!=null&&!(hit.Show.Key==_lastThinKey&&(DateTime.UtcNow-_lastThinBooking).TotalSeconds<2))BookShow?.Invoke(hit.Show);}}
 public ScheduleGrid(){InitializeDragVisuals();DoubleBuffered=true;AutoScroll=false;BackColor=Color.White;Dock=DockStyle.Fill;SetStyle(ControlStyles.ResizeRedraw,true);Controls.Add(_vertical);_vertical.ValueChanged+=(_,_)=>{ViewChanged?.Invoke();Invalidate();HoverShow?.Invoke(null,Point.Empty);};}
 public void RefreshSchedule(){AutoScrollMinSize=new Size(ClientSize.Width,TimelineY(1440));Invalidate();}
 internal static Color CategoryFill(Show show){try{var c=ColorTranslator.FromHtml(show.GenreColor);if(c.IsEmpty)return Color.FromArgb(249,250,252);if(c.R>=230&&c.R-c.G>=25&&c.R-c.B>=25&&Math.Abs(c.G-c.B)<=35)return Color.FromArgb(239,244,253);return Color.FromArgb((int)(255*.82+c.R*.18),(int)(255*.82+c.G*.18),(int)(255*.82+c.B*.18));}catch{return Color.FromArgb(249,250,252);}}
 internal static void DrawRecordingOutline(Graphics graphics,Rectangle bounds,float dpi)
 {
  float thickness=1.5f*dpi/96f;
  if(bounds.Width<=thickness||bounds.Height<=thickness)return;
  var state=graphics.Save();
  try{
   graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
   using var pen=new Pen(Color.FromArgb(207,62,70),thickness){Alignment=System.Drawing.Drawing2D.PenAlignment.Center,LineJoin=System.Drawing.Drawing2D.LineJoin.Miter};
   float inset=thickness/2;
   graphics.DrawRectangle(pen,bounds.X+inset,bounds.Y+inset,bounds.Width-thickness,bounds.Height-thickness);
  }finally{graphics.Restore(state);}
 }
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;_hits.Clear();var order=new[]{"NHK","日テレ","テレ朝","TBS","テレ東","フジ","Eテレ","MX1"};var stations=Shows.Select(s=>s.Station).Distinct().OrderBy(s=>StationOrder.IndexOf(s)<0?int.MaxValue:StationOrder.IndexOf(s)).ThenBy(s=>s,StringComparer.CurrentCulture).ToArray();int width=ColumnWidth,sx=AutoScrollPosition.X,sy=AutoScrollPosition.Y;var start=new DateTimeOffset(Day.Date,TimeSpan.FromHours(9));using var line=new Pen(Color.FromArgb(214,224,237));
 for(int m=0;m<=1440;m+=30){int y=TimelineY(m)+sy;g.DrawLine(line,65,y,Width,y);}
 var dayEnd=start.AddDays(1);var dayShows=Shows.Where(s=>s.Start<dayEnd&&s.End>start).ToLookup(s=>s.Station);
 for(int col=0;col<stations.Length;col++)foreach(var show in dayShows[stations[col]])for(int segment=0;segment<3;segment++){double from=Math.Max(segment*480,(show.Start-start).TotalMinutes),to=Math.Min((segment+1)*480,(show.End-start).TotalMinutes);if(to<=from)continue;var r=new Rectangle(67+col*width+sx,40+(int)(from*3)+segment*30+sy,Math.Max(1,width-4),Math.Max(2,(int)((to-from)*3)-1));if(!r.IntersectsWith(ClientRectangle))continue;using var fill=new SolidBrush(ProgrammeFill(show,Booked.Contains(show.Key),RecordingKeys?.Contains(show.Key)??(Booked.Contains(show.Key)&&show.Start<=DateTimeOffset.Now&&show.End>DateTimeOffset.Now)));g.FillRectangle(fill,r);g.DrawRectangle(line,r);_hits.Add((r,show));DrawProgrammeContents(g,show,r,Font,Booked.Contains(show.Key),Disabled.Contains(show.Key),Assignments.GetValueOrDefault(show.Key),RecordingKeys?.Contains(show.Key)??(Booked.Contains(show.Key)&&show.Start<=DateTimeOffset.Now&&show.End>DateTimeOffset.Now));if((RecordingKeys?.Contains(show.Key)??(Booked.Contains(show.Key)&&show.Start<=DateTimeOffset.Now&&show.End>DateTimeOffset.Now))){DrawRecordingOutline(g,r,DeviceDpi);}else if(Selected?.Key==show.Key){using var pen=new Pen(Color.FromArgb(35,91,159),2);g.DrawRectangle(pen,r);}}
 using var axis=new SolidBrush(Color.FromArgb(246,248,251));g.FillRectangle(axis,0,0,65,Height);for(int m=0;m<=1440;m+=60)TextRenderer.DrawText(g,m==1440?"24:00":start.AddMinutes(m).ToString("HH:mm"),Font,new Rectangle(2,TimelineY(m)+sy+2,62,Font.Height),Color.FromArgb(24,44,66),TextFormatFlags.NoPadding);
 using var header=new SolidBrush(Color.FromArgb(214,214,214));for(int segment=0;segment<3;segment++){int y=segment==0?0:40+segment*480*3+(segment-1)*30+sy,height=segment==0?40:30;g.FillRectangle(header,0,y,Width,height);for(int col=0;col<stations.Length;col++)DrawStationDragBadge(g,stations[col],new Rectangle(65+col*width+sx,y,width,height));}DrawDragOverlay(g);}
 bool IsHeader(Point p){int y=p.Y-AutoScrollPosition.Y;return p.Y<40||Enumerable.Range(1,2).Any(i=>y>=40+i*480*3+(i-1)*30&&y<70+i*480*3+(i-1)*30);}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(_headerDown!=null){UpdateDragVisual(e.Location);Cursor=Cursors.SizeWE;return;}Cursor=IsHeader(e.Location)&&e.X>=65?Cursors.SizeWE:Cursors.Default;var show=e.X<65||IsHeader(e.Location)?null:_hits.LastOrDefault(h=>h.Bounds.Contains(e.Location)).Show;if(show!=null&&CompactFrame(_hits.Last(h=>h.Show.Key==show.Key&&h.Bounds.Contains(e.Location)).Bounds,Font))Cursor=Cursors.Hand;if(show?.Key!=_lastHover){_lastHover=show?.Key;HoverShow?.Invoke(show,e.Location);}}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);_lastHover=null;HoverShow?.Invoke(null,Point.Empty);}
 internal Action<Show,Point>? ContextShow;
 protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(_skipClick){_skipClick=false;return;}var hit=_hits.LastOrDefault(h=>h.Bounds.Contains(e.Location));Selected=e.X<65||IsHeader(e.Location)?null:hit.Show;if(Selected!=null){SelectShow?.Invoke(Selected);if(e.Button==MouseButtons.Right)ContextShow?.Invoke(Selected,e.Location);if(e.Button==MouseButtons.Left&&!Booked.Contains(Selected.Key)&&!Disabled.Contains(Selected.Key)&&CompactFrame(hit.Bounds,Font)&&!(Selected.Key==_lastThinKey&&(DateTime.UtcNow-_lastThinBooking).TotalSeconds<2)){_lastThinKey=Selected.Key;_lastThinBooking=DateTime.UtcNow;BookShow?.Invoke(Selected);}}Invalidate();}
}
