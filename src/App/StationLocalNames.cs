using TsRecommend;
namespace TsScheduler;

internal static class StationLocalNames
{
 internal static void ConfigureGrid(DataGridView grid,string localColumn,string key)
 {
  grid.AllowUserToResizeColumns=true;grid.EditMode=DataGridViewEditMode.EditOnEnter;
  foreach(DataGridViewColumn column in grid.Columns){column.Resizable=DataGridViewTriState.True;column.SortMode=DataGridViewColumnSortMode.NotSortable;column.MinimumWidth=40;column.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;if(DisplayOptions.Current.StationColumnWidths.TryGetValue(key+"/"+column.Name,out int width))column.Width=Math.Clamp(width,40,1000);}
  grid.Columns[localColumn].HeaderText="ローカル名";grid.EditingControlShowing+=(_,e)=>{if(grid.CurrentCell?.ColumnIndex==grid.Columns[localColumn].Index&&e.Control is TextBox editor)editor.BorderStyle=BorderStyle.FixedSingle;};
  grid.CellPainting+=(_,e)=>{if(e.RowIndex<0||e.ColumnIndex<0||grid.Columns[e.ColumnIndex].Name!=localColumn||grid.IsCurrentCellInEditMode&&grid.CurrentCell?.RowIndex==e.RowIndex&&grid.CurrentCell.ColumnIndex==e.ColumnIndex)return;e.Paint(e.ClipBounds,DataGridViewPaintParts.Background|DataGridViewPaintParts.SelectionBackground);var box=Rectangle.Inflate(e.CellBounds,-4,-3);using var fill=new SolidBrush(Color.White);using var border=new Pen(Color.FromArgb(178,188,196));e.Graphics!.FillRectangle(fill,box);e.Graphics.DrawRectangle(border,box);var text=Rectangle.Inflate(box,-4,0);TextRenderer.DrawText(e.Graphics,Convert.ToString(e.FormattedValue)??"",e.CellStyle!.Font??grid.Font,text,Color.FromArgb(35,43,55),TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);e.Handled=true;};
  var timer=new System.Windows.Forms.Timer{Interval=350};void Save(){foreach(DataGridViewColumn column in grid.Columns)DisplayOptions.Current.StationColumnWidths[key+"/"+column.Name]=column.Width;try{DisplayOptions.Current.Save();}catch(IOException){}catch(UnauthorizedAccessException){}}
  timer.Tick+=(_,_)=>{timer.Stop();Save();};grid.ColumnWidthChanged+=(_,_)=>{timer.Stop();timer.Start();};grid.Disposed+=(_,_)=>{if(timer.Enabled){timer.Stop();Save();}timer.Dispose();};
 }
 internal static DataGridView CreateEditor(IEnumerable<string> stations,Action changed)
 {
  var grid=new StableGrid{Width=660,Height=260,AccessibleName="放送局のローカル名",AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,SelectionMode=DataGridViewSelectionMode.CellSelect,MultiSelect=false,CellBorderStyle=DataGridViewCellBorderStyle.None,RowTemplate={Height=32}};
  grid.Columns.Add(new DataGridViewTextBoxColumn{Name="station",HeaderText="局名",ReadOnly=true,Width=340});grid.Columns.Add(new DataGridViewTextBoxColumn{Name="local",HeaderText="ローカル名",Width=260,MaxInputLength=40});
  foreach(string station in stations.DistinctBy(StationBadgeRenderer.NameKey)){int index=grid.Rows.Add(station,StationBadgeRenderer.Alias(station));grid.Rows[index].Tag=station;}
  ConfigureGrid(grid,"local","local-name-editor");bool restoring=false;grid.CellValueChanged+=(_,e)=>{if(restoring||e.RowIndex<0||e.ColumnIndex!=1)return;string station=(string)grid.Rows[e.RowIndex].Tag!;try{StationBadgeRenderer.SetAlias(station,Convert.ToString(grid.Rows[e.RowIndex].Cells[1].Value)??"");changed();}catch(Exception error){restoring=true;grid.Rows[e.RowIndex].Cells[1].Value=StationBadgeRenderer.Alias(station);restoring=false;MessageBox.Show(grid,error.Message,"ローカル名を保存できません");}};
  ModernScrollBar.Attach(grid);return grid;
 }
}
