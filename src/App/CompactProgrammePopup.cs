using TsRecommend;
namespace TsScheduler;
internal sealed class CompactProgrammePopup:Form
{
 readonly ListBox list=new(){Dock=DockStyle.Fill,IntegralHeight=false,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed};
 readonly TextBox detail=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.FromArgb(250,251,252)};
 readonly Label heading=new(){Dock=DockStyle.Top,Height=32,Padding=new Padding(8,6,8,0)};
 readonly Button action=new(){Text="予約／録画の調整",AutoSize=true};
 IReadOnlyList<Show> shows=[];Func<Show,string> describe=_=>"";Func<Show,string> status=_=>"";Action<Show>? activate;

 internal string Details=>detail.Text;
 internal int SelectedIndex{get=>list.SelectedIndex;set=>list.SelectedIndex=value;}
 internal void ActivateSelected(){if(list.SelectedIndex>=0&&list.SelectedIndex<shows.Count){var show=shows[list.SelectedIndex];if(show.End<=DateTimeOffset.Now)return;Hide();activate?.Invoke(show);}}
 internal CompactProgrammePopup()
 {
  Text="短い番組の一覧";ShowInTaskbar=false;FormBorderStyle=FormBorderStyle.FixedToolWindow;StartPosition=FormStartPosition.Manual;Font=new Font("Meiryo UI",10);BackColor=Color.White;Padding=new Padding(1);TopMost=true;
  var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};body.RowStyles.Add(new RowStyle(SizeType.Percent,55));body.RowStyles.Add(new RowStyle(SizeType.Percent,45));body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
  var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,Padding=new Padding(7,5,7,5)};footer.Controls.Add(action);footer.Controls.Add(new Label{Text="選択で詳細・ダブルクリックで予約",AutoSize=true,Margin=new Padding(12,8,0,0)});
  body.Controls.Add(list,0,0);body.Controls.Add(detail,0,1);body.Controls.Add(footer,0,2);Controls.Add(body);Controls.Add(heading);
  list.ItemHeight=30;list.DrawItem+=(_,e)=>{if(e.Index<0||e.Index>=shows.Count)return;e.DrawBackground();var s=shows[e.Index];var text=$"{s.Start:HH:mm}–{s.End:HH:mm}  {status(s)}{s.Title}";TextRenderer.DrawText(e.Graphics,text,Font,Rectangle.Inflate(e.Bounds,-7,0),e.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};
  list.SelectedIndexChanged+=(_,_)=>{if(list.SelectedIndex>=0){var show=shows[list.SelectedIndex];detail.Text=describe(show);detail.SelectionStart=0;action.Enabled=show.End>DateTimeOffset.Now;}};
  list.MouseDoubleClick+=(_,e)=>{if(list.IndexFromPoint(e.Location)>=0)ActivateSelected();};list.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.Handled=true;ActivateSelected();}};action.Click+=(_,_)=>ActivateSelected();

  Deactivate+=(_,_)=>Hide();
 }
 protected override void OnFormClosing(FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}base.OnFormClosing(e);}
 protected override bool ShowWithoutActivation=>false;
 protected override bool ProcessCmdKey(ref Message msg,Keys key){if(key==Keys.Escape){Hide();return true;}return base.ProcessCmdKey(ref msg,key);}
 internal void ShowGroup(Form owner,IReadOnlyList<Show> entries,Show focus,Point point,Func<Show,string> details,Func<Show,string> states,Action<Show> onActivate)
 {
  shows=entries;describe=details;status=states;activate=onActivate;heading.Text=$"{focus.Station}  {entries[0].Start:HH:mm}–{entries[^1].End:HH:mm}  短い番組 {entries.Count}件";
  list.BeginUpdate();list.Items.Clear();foreach(var show in entries)list.Items.Add(show.Key);list.EndUpdate();list.SelectedIndex=Math.Max(0,entries.ToList().FindIndex(s=>s.Key==focus.Key));
  var rows=((TableLayoutPanel)list.Parent!).RowStyles;rows[0].SizeType=SizeType.Absolute;rows[0].Height=Math.Min(entries.Count,10)*list.ItemHeight+8;
  var area=Screen.FromPoint(point).WorkingArea;ClientSize=new Size(Math.Min(700,area.Width-30),Math.Min(Math.Max(340,entries.Count*30+230),Math.Min(580,area.Height-60)));
  Location=new Point(Math.Clamp(point.X+16,area.Left,Math.Max(area.Left,area.Right-Width)),Math.Clamp(point.Y+16,area.Top,Math.Max(area.Top,area.Bottom-Height)));if(!Visible)base.Show(owner);Activate();
 }
}
internal sealed partial class ScheduleGrid
{
 internal Action<IReadOnlyList<Show>,Show,Point>? HoverCompact;
 IReadOnlyList<Show>? compactSource;DateTime compactDay;int compactLineHeight;readonly Dictionary<string,IReadOnlyList<Show>> compactGroups=[];
 internal IReadOnlyList<Show> CompactGroup(Show focus)
 {
  if(!ReferenceEquals(compactSource,Shows)||compactDay!=Day||compactLineHeight!=Font.Height){
   compactSource=Shows;compactDay=Day;compactLineHeight=Font.Height;compactGroups.Clear();var dayStart=new DateTimeOffset(Day.Date,TimeSpan.FromHours(9));
   bool Short(Show s)=>(s.End-s.Start).TotalMinutes>0&&(s.End-s.Start).TotalMinutes*3-1<Font.Height*2;
   foreach(var station in Shows.Where(s=>s.End>dayStart&&s.Start<dayStart.AddDays(1)).DistinctBy(s=>s.Key).GroupBy(s=>s.Station)){
    var all=station.OrderBy(s=>s.Start).ToArray();var run=new List<Show>();
    void Store(){if(run.Count>0){IReadOnlyList<Show> items=run.ToArray();foreach(var item in items)compactGroups[item.Key]=items;run.Clear();}}
    foreach(var item in all){if(!Short(item)){Store();continue;}if(run.Count>0&&Math.Abs((item.Start-run[^1].End).TotalSeconds)>1)Store();run.Add(item);}Store();
   }
  }
  return compactGroups.GetValueOrDefault(focus.Key)??[];
 }
 bool OfferCompact(Show show,Point point,bool force=false){if(HoverCompact==null)return false;var group=CompactGroup(show);if(group.Count<2)return false;string key="compact:"+group[0].Key;if(force||_lastHover!=key){_lastHover=key;HoverShow?.Invoke(null,Point.Empty);HoverCompact(group,show,point);}return true;}
}
