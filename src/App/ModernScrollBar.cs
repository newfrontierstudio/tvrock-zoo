using System.Drawing.Drawing2D;
namespace TsScheduler;
internal sealed class ModernScrollBar:Control
{
    internal static void Attach(DataGridView grid)
    {
        var bars=new Dictionary<ScrollBar,ModernScrollBar>();bool syncing=false;
        void Fit(){if(syncing||grid.IsDisposed)return;syncing=true;try{foreach(var native in grid.Controls.OfType<ScrollBar>().ToArray()){
            if(!bars.TryGetValue(native,out var modern)){modern=new ModernScrollBar{Horizontal=native is HScrollBar};bars.Add(native,modern);grid.Controls.Add(modern);var target=native;modern.ValueChanged+=(_,_)=>{if(syncing)return;target.Value=Math.Clamp(modern.Value,target.Minimum,Math.Max(target.Minimum,target.Maximum-target.LargeChange+1));typeof(ScrollBar).GetMethod("OnScroll",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(target,[new ScrollEventArgs(ScrollEventType.ThumbPosition,target.Value,target is HScrollBar?ScrollOrientation.HorizontalScroll:ScrollOrientation.VerticalScroll)]);};native.ValueChanged+=(_,_)=>Fit();native.VisibleChanged+=(_,_)=>Fit();}
            modern.Bounds=native.Bounds;modern.LargeChange=native.LargeChange;modern.Maximum=native.Maximum;modern.Value=native.Value;modern.Visible=native.Visible;modern.BringToFront();
        }}finally{syncing=false;}}
        grid.Layout+=(_,_)=>Fit();grid.HandleCreated+=(_,_)=>Fit();grid.Scroll+=(_,_)=>Fit();Fit();
    }
    int maximum=100,large=10,value,grab;bool dragging;
    internal bool Horizontal{get;set;}
    internal int SmallChange{get;set;}=24;
    internal int Maximum{get=>maximum;set{maximum=Math.Max(0,value);Value=this.value;Invalidate();}}
    internal int LargeChange{get=>large;set{large=Math.Max(1,value);Value=this.value;Invalidate();}}
    internal int Value{get=>value;set{int next=Math.Clamp(value,0,Math.Max(0,maximum-large+1));if(next==this.value)return;this.value=next;Invalidate();ValueChanged?.Invoke(this,EventArgs.Empty);}}
    internal event EventHandler? ValueChanged;
    internal ModernScrollBar(){Width=16;Height=16;BackColor=Color.White;TabStop=true;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    int Length=>Horizontal?Width:Height;
    internal Rectangle Thumb{get{int track=Math.Max(1,Length-4),size=Math.Min(track,Math.Max(30,(int)((double)large/Math.Max(large,maximum+1)*track))),range=Math.Max(0,maximum-large+1);int pos=2+(range==0?0:(int)((double)value/range*(track-size)));int thickness=Math.Max(4,(int)Math.Round(5*DeviceDpi/96d));return Horizontal?new Rectangle(pos,(Height-thickness)/2,size,thickness):new Rectangle((Width-thickness)/2,pos,thickness,size);}}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(maximum<large)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var shape=RoundedControls.Shape(Thumb,4);using var fill=new SolidBrush(dragging?Color.FromArgb(110,110,110):Color.FromArgb(148,148,148));e.Graphics.FillPath(fill,shape);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();int p=Horizontal?e.X:e.Y,start=Horizontal?Thumb.X:Thumb.Y;if(p>=start&&p<=start+(Horizontal?Thumb.Width:Thumb.Height)){grab=p-start;dragging=true;Capture=true;}else Value+=p<start?-large:large;Invalidate();}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;int size=Horizontal?Thumb.Width:Thumb.Height;Value=(int)((double)((Horizontal?e.X:e.Y)-grab-2)/Math.Max(1,Length-4-size)*Math.Max(0,maximum-large+1));}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;Invalidate();}
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);Value-=e.Delta;}
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);switch(e.KeyCode){case Keys.Up:case Keys.Left:Value-=SmallChange;break;case Keys.Down:case Keys.Right:Value+=SmallChange;break;case Keys.PageUp:Value-=large;break;case Keys.PageDown:Value+=large;break;case Keys.Home:Value=0;break;case Keys.End:Value=maximum;break;}}
}
