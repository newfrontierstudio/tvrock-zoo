namespace TsScheduler;

internal sealed class StableGrid:DataGridView
{
    internal StableGrid(){DoubleBuffered=true;SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
    int freezes,resizing=-1,origin,width,guide=-1;bool suppressHeader;
    sealed class Scope(StableGrid grid):IDisposable{public void Dispose(){if(--grid.freezes==0&&grid.IsHandleCreated){SendMessage(grid.Handle,11,(IntPtr)1,IntPtr.Zero);grid.Invalidate(true);}}}
    internal IDisposable Freeze(){if(freezes++==0&&IsHandleCreated)SendMessage(Handle,11,IntPtr.Zero,IntPtr.Zero);return new Scope(this);}
    int Edge(Point p){if(!AllowUserToResizeColumns||p.Y<0||p.Y>=ColumnHeadersHeight)return -1;foreach(DataGridViewColumn column in Columns){if(!column.Visible||column.Resizable==DataGridViewTriState.False)continue;var bounds=GetColumnDisplayRectangle(column.Index,true);if(bounds.Width>0&&Math.Abs(bounds.Right-p.X)<=5)return column.Index;}return -1;}
    protected override void OnMouseDown(MouseEventArgs e){int edge=Edge(e.Location);if(e.Button==MouseButtons.Left&&edge>=0){resizing=edge;origin=e.X;width=Columns[edge].Width;guide=GetColumnDisplayRectangle(edge,false).Right;Capture=true;Invalidate();return;}base.OnMouseDown(e);}
    protected override void OnMouseMove(MouseEventArgs e){if(resizing>=0){guide=GetColumnDisplayRectangle(resizing,false).Left+Math.Max(Columns[resizing].MinimumWidth,width+e.X-origin);Invalidate();return;}Cursor=Edge(e.Location)>=0?Cursors.VSplit:Cursors.Default;base.OnMouseMove(e);}
    protected override void OnMouseUp(MouseEventArgs e){if(resizing>=0){var column=Columns[resizing];int target=Math.Max(column.MinimumWidth,width+e.X-origin);resizing=-1;guide=-1;column.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;column.Width=target;suppressHeader=true;Capture=false;Invalidate();return;}base.OnMouseUp(e);}
    protected override void OnColumnHeaderMouseClick(DataGridViewCellMouseEventArgs e){if(suppressHeader){suppressHeader=false;return;}base.OnColumnHeaderMouseClick(e);}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&resizing>=0){resizing=-1;guide=-1;Invalidate();}}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(guide>=0){using var pen=new Pen(Color.Black,1);e.Graphics.DrawLine(pen,guide,0,guide,ClientSize.Height);}}
}
