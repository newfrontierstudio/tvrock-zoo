using System.Drawing.Drawing2D;
namespace TsScheduler;
internal sealed class ModernScrollBar:Control
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridView,object> attached=new();
    internal static void Attach(DataGridView grid)
    {
        if(attached.TryGetValue(grid,out _))return;attached.Add(grid,new object());
        bool allowHorizontal=grid.ScrollBars is ScrollBars.Both or ScrollBars.Horizontal;
        grid.ScrollBars=ScrollBars.None;
        var vertical=new ModernScrollBar();var horizontal=new ModernScrollBar{Horizontal=true};grid.Controls.Add(vertical);grid.Controls.Add(horizontal);bool syncing=false;
        IEnumerable<DataGridViewRow> Rows()=>grid.Rows.Cast<DataGridViewRow>().Where(r=>r.Visible&&!r.Frozen);
        void Fit(){if(syncing||grid.IsDisposed)return;syncing=true;try{
            if(grid.ScrollBars!=ScrollBars.None){allowHorizontal=grid.ScrollBars is ScrollBars.Both or ScrollBars.Horizontal;grid.ScrollBars=ScrollBars.None;}
            int scale=Math.Max(10,(int)Math.Round(12*grid.DeviceDpi/96d));int top=grid.ColumnHeadersVisible?grid.ColumnHeadersHeight:0;
            int total=Rows().Sum(r=>r.Height),columns=grid.Columns.Cast<DataGridViewColumn>().Where(c=>c.Visible&&!c.Frozen).Sum(c=>c.Width);
            bool v=total>Math.Max(1,grid.ClientSize.Height-top);bool h=allowHorizontal&&columns>grid.ClientSize.Width-(v?scale:0);
            vertical.Bounds=new Rectangle(Math.Max(0,grid.ClientSize.Width-scale),top,scale,Math.Max(0,grid.ClientSize.Height-top-(h?scale:0)));
            vertical.Maximum=Math.Max(0,total-1);vertical.LargeChange=Math.Max(1,vertical.Height);vertical.SmallChange=Math.Max(1,grid.RowTemplate.Height);vertical.Value=Rows().TakeWhile(r=>r.Index!=grid.FirstDisplayedScrollingRowIndex).Sum(r=>r.Height);vertical.Visible=v;
            horizontal.Bounds=new Rectangle(0,Math.Max(0,grid.ClientSize.Height-scale),Math.Max(0,grid.ClientSize.Width-(v?scale:0)),scale);horizontal.Maximum=Math.Max(0,columns-1);horizontal.LargeChange=Math.Max(1,horizontal.Width);horizontal.Value=grid.HorizontalScrollingOffset;horizontal.Visible=h;
            vertical.BringToFront();horizontal.BringToFront();
        }finally{syncing=false;}}
        vertical.ValueChanged+=(_,_)=>{if(syncing)return;int desired=vertical.Value,offset=0;foreach(var row in Rows()){if(offset+row.Height>desired){grid.FirstDisplayedScrollingRowIndex=row.Index;break;}offset+=row.Height;}Fit();};
        horizontal.ValueChanged+=(_,_)=>{if(!syncing)grid.HorizontalScrollingOffset=horizontal.Value;};
        grid.MouseWheel+=(_,e)=>{if(e is HandledMouseEventArgs handled)handled.Handled=true;var rows=Rows().ToList();if(rows.Count==0)return;int first=Math.Max(0,rows.FindIndex(r=>r.Index==grid.FirstDisplayedScrollingRowIndex));int step=(int)Math.Round(e.Delta/120d*Math.Max(1,SystemInformation.MouseWheelScrollLines));grid.FirstDisplayedScrollingRowIndex=rows[Math.Clamp(first-step,0,rows.Count-1)].Index;Fit();};
        grid.Layout+=(_,_)=>Fit();grid.HandleCreated+=(_,_)=>Fit();grid.Scroll+=(_,_)=>Fit();grid.VisibleChanged+=(_,_)=>Fit();grid.SizeChanged+=(_,_)=>Fit();grid.RowsAdded+=(_,_)=>Fit();grid.RowsRemoved+=(_,_)=>Fit();grid.RowHeightChanged+=(_,_)=>Fit();grid.ColumnWidthChanged+=(_,_)=>Fit();grid.Paint+=(_,_)=>Fit();Fit();
    }
    int maximum=100,large=10,value,grab;bool dragging,hover;
    internal bool Horizontal{get;set;}
    internal int SmallChange{get;set;}=24;
    internal int Maximum{get=>maximum;set{maximum=Math.Max(0,value);Value=this.value;Invalidate();}}
    internal int LargeChange{get=>large;set{large=Math.Max(1,value);Value=this.value;Invalidate();}}
    internal int Value{get=>value;set{int next=Math.Clamp(value,0,Math.Max(0,maximum-large+1));if(next==this.value)return;this.value=next;Invalidate();ValueChanged?.Invoke(this,EventArgs.Empty);}}
    internal event EventHandler? ValueChanged;
    internal ModernScrollBar(){Width=12;Height=12;BackColor=Color.White;TabStop=true;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    int Length=>Horizontal?Width:Height;
    internal Rectangle Thumb{get{int track=Math.Max(1,Length-4),size=Math.Min(track,Math.Max(30,(int)((double)large/Math.Max(large,maximum+1)*track))),range=Math.Max(0,maximum-large+1);int pos=2+(range==0?0:(int)((double)value/range*(track-size)));int thickness=Math.Max(4,(int)Math.Round(5*DeviceDpi/96d));return Horizontal?new Rectangle(pos,(Height-thickness)/2,size,thickness):new Rectangle((Width-thickness)/2,pos,thickness,size);}}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(maximum<large)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var shape=RoundedControls.Shape(Thumb,4);using var fill=new SolidBrush(dragging?Color.FromArgb(110,110,110):hover?Color.FromArgb(139,139,139):Color.FromArgb(184,184,184));e.Graphics.FillPath(fill,shape);}
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();int p=Horizontal?e.X:e.Y,start=Horizontal?Thumb.X:Thumb.Y;if(p>=start&&p<=start+(Horizontal?Thumb.Width:Thumb.Height)){grab=p-start;dragging=true;Capture=true;}else Value+=p<start?-large:large;Invalidate();}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;int size=Horizontal?Thumb.Width:Thumb.Height;Value=(int)((double)((Horizontal?e.X:e.Y)-grab-2)/Math.Max(1,Length-4-size)*Math.Max(0,maximum-large+1));}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;Invalidate();}
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);Value-=(int)Math.Round(e.Delta/120d*SmallChange*Math.Max(1,SystemInformation.MouseWheelScrollLines));}
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);switch(e.KeyCode){case Keys.Up:case Keys.Left:Value-=SmallChange;break;case Keys.Down:case Keys.Right:Value+=SmallChange;break;case Keys.PageUp:Value-=large;break;case Keys.PageDown:Value+=large;break;case Keys.Home:Value=0;break;case Keys.End:Value=maximum;break;}}
}
